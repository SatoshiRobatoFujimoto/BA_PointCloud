﻿using ObjectCreation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CloudData
{
    /* Resembles a node of the nested octree
    */
    public class Node : IEnumerable<Node>
    {
        //filename without the r. for example 073. identifieing the node in the tree
        private string name;
        //MetaData of the cloud
        private PointCloudMetaData metaData;
        //BoundingBox of this node
        private BoundingBox boundingBox;
        //The vertices this node is containing. Null before being set. Will be set to null after creating gameobjects
        private Vector3[] verticesToStore;
        //The colors for the nodes. colors and vertices should always have the same length! Null before being set. Will be set to null after creating gameobjects
        private Color[] colorsToStore;
        //Array of children. May contain null-elements if no child on this index exists
        private Node[] children = new Node[8];
        //Parent-element. May be null at the root.
        private Node parent;
        //PointCount, read from hierarchy-file
        private uint pointCount = 0;

        //A status flag. Can be used by the renderer, doesn't have to be!
        //The meaning and use of the NodeStatus is therefore dependent from the used renderer and does not have to be used consistently
        private byte nodeStatus = CloudData.NodeStatus.UNDEFINED;

        //List containing the gameobjects resembling this node
        private List<GameObject> gameObjects = new List<GameObject>();

        public Node(string name, PointCloudMetaData metaData, BoundingBox boundingBox, Node parent)
        {
            this.name = name;
            this.metaData = metaData;
            this.boundingBox = boundingBox;
            this.parent = parent;
        }

        public int GetLevel()
        {
            return name.Length;
        }

        //Creates the Game Object(s) containing the points of this node
        //Does not happen in the constructor, as gameobjects should be created on the main thread (valled via update)
        //Vertices and Colors have to be set before. Vertices and Colors are removed from this object after the creation of the GameObjects
        public void CreateGameObjects(MeshConfiguration configuration)
        {
            int max = configuration.GetMaximumPointsPerMesh();
            if (verticesToStore.Length <= max) {
                gameObjects.Add(configuration.CreateGameObject(metaData.cloudName + "/" + "r" + name, verticesToStore, colorsToStore, boundingBox));
                verticesToStore = null;
                colorsToStore = null;
            } else { 
                int amount = Math.Min(max, verticesToStore.Length);        //Typecast: As max is an int, the value cannot be out of range
                int index = 0; //name index
                while (amount > 0) {
                    Vector3[] vertices = verticesToStore.Take(amount).ToArray();
                    Color[] colors = colorsToStore.Take(amount).ToArray(); ;
                    verticesToStore = verticesToStore.Skip(amount).ToArray();
                    colorsToStore = colorsToStore.Skip(amount).ToArray();
                    gameObjects.Add(configuration.CreateGameObject(metaData.cloudName + "/" + "r" + name + "_" + index, vertices, colors, boundingBox));
                    amount = Math.Min(max, verticesToStore.Length);
                    index++;
                }
                verticesToStore = null;
                colorsToStore = null;
            }
        }

        /* Creates a box game object with the shape of the bounding box of this node
         */
        public void CreateBoundingBoxGameObject()
        {
            GameObject box = UnityEngine.Object.Instantiate<GameObject>(Resources.Load<GameObject>("Prefabs/BoundingBoxPrefab"));
            box.transform.Translate((boundingBox.Min() + (boundingBox.Size() / 2)).ToFloatVector());
            box.transform.localScale = boundingBox.Size().ToFloatVector();
        }

        /* As CreateGameObjects, but it also creates the gameobjects of the children recursively
         */
        public void CreateAllGameObjects(MeshConfiguration configuration)
        {
            CreateGameObjects(configuration);
            for (int i = 0; i < 8; i++)
            {
                if (children[i] != null)
                {
                    children[i].CreateAllGameObjects(configuration);
                }
            }
        }

        /* Removes the gameobjects again.
         * Given MeshConfiguration should be the same that has been used for creation
         */
        public void RemoveGameObjects(MeshConfiguration configuration, bool restorePoints)
        {
            if (gameObjects.Count == 1) {
                Vector3[] vertices;
                Color[] colors;
                configuration.RemoveGameObject(gameObjects[0], out vertices, out colors);
                if (restorePoints) {
                    verticesToStore = vertices;
                    colorsToStore = colors;
                }
            } else if (gameObjects.Count > 1) {
                if (restorePoints) {
                    verticesToStore = new Vector3[pointCount];
                    colorsToStore = new Color[pointCount];
                }
                int offset = 0;
                foreach (GameObject go in gameObjects) {
                    Vector3[] vertices;
                    Color[] colors;
                    configuration.RemoveGameObject(go, out vertices, out colors);
                    if (restorePoints) {
                        for (int i = 0; i < vertices.Length; i++) {
                            verticesToStore[offset + i] = vertices[i];
                            colorsToStore[offset + i] = colors[i];
                        }
                    }
                    offset += vertices.Length;
                }
            }
            gameObjects.Clear();
        }
        
        /* Sets the point data to be stored.
         * Throws an exception if gameobjects already exist or vertices or colors are null or their length do not match
         */
        public void SetPoints(Vector3[] vertices, Color[] colors)
        {
            if (gameObjects.Count != 0)
            {
                throw new ArgumentException("GameObjects already created!");
            }
            if (vertices == null || colors == null || vertices.Length != colors.Length)
            {
                throw new ArgumentException("Invalid data given!");
            }
            verticesToStore = vertices;
            colorsToStore = colors;
            pointCount = (uint)vertices.Length;
        }

        /* Deletes the loaded Vertex- and Color-Information (Vertex-Count stays stored however)
         */
        public void ForgetPoints() {
            verticesToStore = null;
            colorsToStore = null;
        }

        /* Wether there are points which can be used for the creation of a game object
         */
        public bool HasPointsToRender()
        {
            return verticesToStore != null && colorsToStore != null;
        }

        public bool HasGameObjects()
        {
            return gameObjects.Count != 0;
        }

        public void SetChild(int index, Node node)
        {
            children[index] = node;
        }

        public Node GetChild(int index)
        {
            return children[index];
        }

        public bool HasChild(int index)
        {
            return children[index] != null;
        }

        /* This enumerator enables enumerating through the children of this Node
         */
        public IEnumerator<Node> GetEnumerator()
        {
            return new ChildEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator(); //...?
        }

        //Enumerator for the children
        private class ChildEnumerator : IEnumerator<Node>
        {
            Node outer;

            public ChildEnumerator(Node n)
            {
                outer = n;
            }

            int nextIndex = -1;

            public Node Current
            {
                get
                {
                    if (nextIndex < 0 || nextIndex >= 8)
                    {
                        throw new InvalidOperationException();
                    }
                    return outer.children[nextIndex];
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return Current;
                }
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                do
                {
                    ++nextIndex;
                }
                while (nextIndex < 8 && outer.children[nextIndex] == null);
                if (nextIndex == 8) return false;
                return true;
            }

            public void Reset()
            {
                nextIndex = -1;
            }
        }
        
        public string Name
        {
            get { return name; }
        }

        public BoundingBox BoundingBox
        {
            get
            {
                return boundingBox;
            }
        }

        public Node Parent
        {
            get
            {
                return parent;
            }

            set
            {
                parent = value;
            }
        }

        //Number of points given the last time SetPoints was called. Or 0 if it hasn't been called
        public uint PointCount
        {
            get
            {
                return pointCount;
            }
        }
        
        public PointCloudMetaData MetaData {
            get { return metaData; }
        }

        public byte NodeStatus {
            get {
                return nodeStatus;
            }

            set {
                nodeStatus = value;
            }
        }

        public override string ToString()
        {
            return "Node: r" + Name;
        }
    }


}

