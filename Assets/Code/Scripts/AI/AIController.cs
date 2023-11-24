using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace AI {
    public class DodgePoint {
        // Class representing dodge points for AI
        public Vector3 offset;
        public List<DodgePoint> children;
        public List<DodgePoint> parents;

        public GameObject offsetObject;

        private Vector3 originalPosition;

        public DodgePoint(Vector3 position, GameObject offsetObject) {
            if (offsetObject != null) {
                this.offset = offsetObject.transform.position - position;
                this.offsetObject = offsetObject;
            }
            
            originalPosition = position;

            children = new List<DodgePoint>();
            parents = new List<DodgePoint>();
        }

        public Vector3 GetPosition() {
            // Get the position of the dodge point based on offset and offsetObject
            if (offsetObject == null)
                return Vector3.zero;

            if (offsetObject.CompareTag("Enemy"))
                return originalPosition;
            else
                return offsetObject.transform.position - offset;
        }

        public Vector2 GetLocalPosition() {
            return new Vector2(Mathf.Sign(offset.x), Mathf.Sign(offset.y));
        }
    }

    public static class Scanner {

        public static Transform[] GetLines(Vector3 position) {
            // Get lines from a given position for AI scanning
            Transform[] lines = new Transform[4];
            RaycastHit hit;

            if (Physics.Raycast(new Ray(position, Vector3.down), out hit, Mathf.Infinity, LayerMask.GetMask("Water"))) {
                Tile tileComponent = hit.transform.gameObject.GetComponent<Tile>();
                if (tileComponent != null) {
                    Transform[] tileLines = tileComponent.carLines;
                    return tileLines;
                }
                else {                    
                    Debug.LogWarning("Tile component not found on the hit object.");
                    return null;
                }
            }
            else {
                return null;
            }
        }

        public static Collider[] GetOverlay(Vector3 position, float passLength) {
            return Physics.OverlapBox(position, new Vector3(1, 10f, passLength / 2), Quaternion.identity, LayerMask.GetMask("WorldObject"));
        }
    }

    

    public class AIController : MonoBehaviour {
        [SerializeField] private bool showPointRelations;
        [SerializeField] private float dodgeAnggleLimit;
        [SerializeField] private float collisionRadius;
        public float passLength;
        private int updateFrequency = 60;

        public List<DodgePoint> dodgePoints;
        public List<GameObject> activeCars;

        void Start() {
            dodgePoints = new List<DodgePoint>();
            activeCars = new List<GameObject>();

        }


        private void OnDrawGizmosSelected() {
            // Visualize dodge points and connections in the Unity editor
            if (dodgePoints != null) {

                foreach (DodgePoint point in dodgePoints) {
                    if (point.offsetObject == null)
                        continue;

                    Vector3 position = point.offsetObject.transform.position - point.offset;

                    if (point.offsetObject) {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere(position, 0.5f);

                        if (showPointRelations) {
                            foreach (DodgePoint point2 in dodgePoints) {
                                if (point2.offsetObject == null)
                                    continue;
                                Vector3 position2 = point2.offsetObject.transform.position - point2.offset;
                                if (position2.z > position.z && Mathf.Abs(Vector3.Angle(position2 - position, Vector3.forward)) < dodgeAnggleLimit) {
                                    if (!Physics.CheckCapsule(position, position2, collisionRadius, LayerMask.GetMask("WorldObject"))) {
                                        Gizmos.color = Color.gray;
                                        Gizmos.DrawLine(position, position2);
                                    }
                                }


                            }
                        }
                    }
                }




            }
        }

        private void Update() {
            if (Time.frameCount % updateFrequency == 0) {
                UpdateDodgePoints(null);
            }            

        }

        public void UpdateDodgePoints(GameObject car) {
            List<DodgePoint> pointsToRemove = new List<DodgePoint>();

            if (car != null)
                dodgePoints.AddRange(car.GetComponent<Obstacle>().GetDodgePoints(passLength));                       

            foreach (DodgePoint point in dodgePoints) {
                if (point.offsetObject == null || Scanner.GetOverlay(point.GetPosition(), passLength).Length > 0)
                    pointsToRemove.Add(point);
            }

            foreach (DodgePoint point in pointsToRemove) {
                dodgePoints.Remove(point);
            } 

        }

        private bool AreDodgePointsEqual(List<DodgePoint> list1, List<DodgePoint> list2) {
            if (list1.Count != list2.Count) {
                return false;
            }

            for (int i = 0; i < list1.Count; i++) {
                if (list1[i] != list2[i]) {
                    return false;
                }
            }

            return true;
        }

        private void ConnectPoints() {            
            foreach (DodgePoint point in dodgePoints) {                
                point.parents = new List<DodgePoint>();
            }

            foreach (DodgePoint point1 in dodgePoints) {
                point1.children = new List<DodgePoint>();
                Vector3 position1 = point1.GetPosition();

                foreach (DodgePoint point2 in dodgePoints) {
                    Vector3 position2 = point2.GetPosition();

                    if (position2.z > position1.z && Mathf.Abs(Vector3.Angle(position2 - position1, Vector3.forward)) < dodgeAnggleLimit) {
                        if (!Physics.CheckCapsule(position1, position2, collisionRadius, LayerMask.GetMask("WorldObject"))) {
                            point1.children.Add(point2);
                            point2.parents.Add(point1);
                        }
                    }
                }
            }
        }

        public List<DodgePoint> GetPath(DodgePoint rootNode) {
            // Find a path from the root node to a target node with a maximum Z value
            dodgePoints.Insert(0, rootNode);

            ConnectPoints();

            //targetNode = 

            DodgePoint targetNode = null;

            foreach(DodgePoint point in dodgePoints.OrderByDescending(point => point.GetPosition().z)) {
                
                if (FindShortestPath(rootNode, point) != null) {
                    targetNode = point;
                    break;
                }

                
            }

            if (targetNode != null) {
                List<DodgePoint> shortestPath = FindShortestPath(rootNode, targetNode);
                dodgePoints.Remove(rootNode);
                if (shortestPath != null) {
                    return shortestPath;
                }
                else {
                    Debug.Log("TargetNode is the root node");
                }
            }
            else {
                dodgePoints.Remove(rootNode);
                Debug.Log("Target node not found.");
                //EditorApplication.isPaused = true;
                return null;
            }
            dodgePoints.Remove(rootNode);
            return null;
        }        

        public List<List<DodgePoint>> GetAllPaths(DodgePoint rootNode) {
            // Find a path from the root node to a target node with a maximum Z value
            dodgePoints.Insert(0, rootNode);

            ConnectPoints();

            //targetNode = 

            DodgePoint targetNode = null;

            foreach (DodgePoint point in dodgePoints.OrderByDescending(point => point.GetPosition().z)) {

                if (FindShortestPath(rootNode, point) != null) {
                    targetNode = point;
                    break;
                }


            }

            if (targetNode != null) {
                List<List<DodgePoint>> shortestPaths = FindAllPaths(rootNode, targetNode);

                if (shortestPaths != null) {
                    dodgePoints.Remove(rootNode);
                    return shortestPaths;
                }
                else {
                    dodgePoints.Remove(rootNode);
                    Debug.Log("TargetNode is the root node");
                }
            }
            else {
                dodgePoints.Remove(rootNode);
                Debug.Log("Target node not found.");
                //EditorApplication.isPaused = true;
                return null;
            }
            dodgePoints.Remove(rootNode);
            return null;
        }

        private DodgePoint FindMaxZNode(DodgePoint startNode, float nodeMaxZ) {
            // Find the node with the maximum Z value
            if (startNode.GetPosition().z >= nodeMaxZ && startNode.children.Count != 0 ) {
                return startNode;
            }

            foreach (DodgePoint child in startNode.children) {
                DodgePoint foundNode = FindMaxZNode(child, nodeMaxZ);

                if (foundNode != null) {
                    return foundNode;
                }
            }

            return null;
        }

        List<DodgePoint> FindShortestPath(DodgePoint root, DodgePoint target) {
            Queue<DodgePoint> queue = new Queue<DodgePoint>();
            Dictionary<DodgePoint, DodgePoint> parentMap = new Dictionary<DodgePoint, DodgePoint>();

            queue.Enqueue(root);
            parentMap[root] = null;

            while (queue.Count > 0) {
                DodgePoint currentNode = queue.Dequeue();

                if (currentNode == target) {
                    // Reconstruct the path
                    List<DodgePoint> path = new List<DodgePoint>();
                    while (currentNode != null) {
                        path.Insert(0, currentNode);
                        currentNode = parentMap[currentNode];
                    }
                    return path;
                }

                foreach (DodgePoint child in currentNode.children) {
                    if (!parentMap.ContainsKey(child)) {
                        queue.Enqueue(child);
                        parentMap[child] = currentNode;
                    }
                }
            }

            return null; // Target node not found
        }
        public List<List<DodgePoint>> FindAllPaths(DodgePoint root, DodgePoint target) {
            List<List<DodgePoint>> allPaths = new List<List<DodgePoint>>();
            List<DodgePoint> currentPath = new List<DodgePoint>();

            DFS(root, target, currentPath, allPaths);

            return allPaths;
        }

        private void DFS(DodgePoint currentNode, DodgePoint targetNode, List<DodgePoint> currentPath, List<List<DodgePoint>> allPaths) {
            if (currentNode == null)
                return;

            currentPath.Add(currentNode);

            if (currentNode == targetNode) {
                // Found a path to the target node
                allPaths.Add(new List<DodgePoint>(currentPath));
            }
            else {
                foreach (DodgePoint child in currentNode.children) {
                    DFS(child, targetNode, currentPath, allPaths);
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
        }
    }

   
}  