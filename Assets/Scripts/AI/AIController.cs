using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AI {
    // Represents a point that an AI can dodge to, tracking its position relative to an offset object
    public class DodgePoint {
        public Vector3 offset { get; private set; }  // The offset from the original position, used for calculations
        public List<DodgePoint> Children { get; private set; }  // Points that this point can connect to for dodging
        public GameObject offsetObject { get; private set; }  // The object that the offset is calculated from

        private Vector3 originalPosition;

        // Constructor to create a DodgePoint with its position and related object
        public DodgePoint(Vector3 position, GameObject offsetObject) {
            offset = offsetObject != null ? offsetObject.transform.position - position : Vector3.zero;
            this.offsetObject = offsetObject;
            originalPosition = position;
            Children = new List<DodgePoint>();
        }

        // Returns the current position of the DodgePoint
        public Vector3 GetPosition() {
            return offsetObject == null ? Vector3.zero : offsetObject.CompareTag("Enemy") ? originalPosition : offsetObject.transform.position - offset;
        }

        // Returns the local position of the DodgePoint as a 2D vector
        public Vector2 GetLocalPosition() {
            return new Vector2(Mathf.Sign(offset.x), Mathf.Sign(offset.y));
        }
    }

    // A static utility class for scanning the environment to find objects and lines
    public static class Scanner {
        private const string WaterLayer = "Water";
        private const string WorldObjectLayer = "WorldObject";

        // Returns the car lines associated with the tile at the specified position
        public static Transform[] GetLines(Vector3 position) {
            RaycastHit hit;

            if (Physics.Raycast(new Ray(position, Vector3.down), out hit, Mathf.Infinity, LayerMask.GetMask(WaterLayer))) {
                Tile tileComponent = hit.transform.gameObject.GetComponent<Tile>();
                return tileComponent?.carLines;
            }

            Debug.DrawRay(position, Vector3.up, Color.yellow);
            Debug.Break();
            Debug.LogWarning("Tile component not found on the hit object.");
            return null;
        }

        // Returns any colliders that overlap with the given position, within the specified pass length
        public static Collider[] GetOverlay(Vector3 position, float passLength) {
            return Physics.OverlapBox(position, new Vector3(1, 10f, passLength / 2), Quaternion.identity, LayerMask.GetMask(WorldObjectLayer));
        }
    }

    public class AIController : MonoBehaviour {
        [SerializeField] private bool showPointRelations;  // Whether to show lines connecting related dodge points
        [Range(0, 90)]
        [SerializeField] public float dodgeAngleLimit;  // The maximum angle that an AI can dodge
        [SerializeField] private float collisionRadius;  // The radius used for collision detection between points                

        public List<DodgePoint> dodgePoints;  // A list of all dodge points currently tracked by the AI
        private List<GameObject> activeCars;  // A list of all active cars in the scene

        // Initialize the dodge points and active cars lists
        private void Start() {
            dodgePoints = new List<DodgePoint>();
            activeCars = new List<GameObject>();
        }

        // Draws gizmos in the editor to visualize dodge points and their connections
        private void OnDrawGizmosSelected() {
            if (dodgePoints != null) {
                foreach (DodgePoint point in dodgePoints) {
                    if (point.offsetObject == null) continue;

                    Vector3 position = point.offsetObject.transform.position - point.offset;

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(position, 0.5f);

                    if (showPointRelations) {
                        foreach (DodgePoint point2 in dodgePoints.Where(p => p.offsetObject != null)) {
                            Vector3 position2 = point2.offsetObject.transform.position - point2.offset;
                            if (position2.z > position.z && Mathf.Abs(Vector3.Angle(position2 - position, Vector3.forward)) < dodgeAngleLimit &&
                                !Physics.CheckCapsule(position, position2, collisionRadius, LayerMask.GetMask("WorldObject"))) {
                                Gizmos.color = Color.gray;
                                Gizmos.DrawLine(position, position2);
                            }
                        }
                    }
                }
            }
        }

        
        private void Update() {            
        }                

        // Connects dodge points that can be reached from each other within the dodge angle limit
        private void ConnectPoints() {
            foreach (DodgePoint point in dodgePoints) {
                if (point.GetPosition().z > dodgePoints[0].GetPosition().z && Mathf.Abs(Vector3.Angle(point.GetPosition() - dodgePoints[0].GetPosition(), Vector3.forward)) < dodgeAngleLimit &&
                    !Physics.CheckCapsule(dodgePoints[0].GetPosition(), point.GetPosition(), collisionRadius, LayerMask.GetMask("WorldObject"))) {
                    dodgePoints[0].Children.Add(point);
                }
            }
        }

        // Finds the next dodge point along a path from a given root node
        public DodgePoint GetPath(DodgePoint rootNode) {
            if (dodgePoints.Count == 0) return null;            

            dodgePoints.Insert(0, rootNode);

            ConnectPoints();

            DodgePoint targetNode = null;

            if (dodgePoints[0].Children.Count != 0) {

                targetNode = dodgePoints[0].Children[Random.Range(0, dodgePoints[0].Children.Count - 1)];
            }

            if (targetNode != null) {
                dodgePoints.Remove(rootNode);
                return targetNode;
            }
            else {
                dodgePoints.Remove(rootNode);
                Debug.Log("Target node not found.");
                return null;
            }
        }

        // Finds the node with the maximum Z position starting from a given node
        private DodgePoint FindMaxZNode(DodgePoint startNode, float nodeMaxZ) {
            while (startNode.GetPosition().z < nodeMaxZ || startNode.Children.Count == 0) {
                startNode = startNode.Children.FirstOrDefault(child => child.GetPosition().z >= nodeMaxZ);
                if (startNode == null) {
                    break;
                }
            }

            return startNode;
        }

        // Finds the shortest path from a root node to a target node
        private List<DodgePoint> FindShortestPath(DodgePoint root, DodgePoint target) {
            Queue<DodgePoint> queue = new Queue<DodgePoint>();
            Dictionary<DodgePoint, DodgePoint> parentMap = new Dictionary<DodgePoint, DodgePoint>();

            queue.Enqueue(root);
            parentMap[root] = null;

            while (queue.Count > 0) {
                DodgePoint currentNode = queue.Dequeue();

                if (currentNode == target) {
                    List<DodgePoint> path = new List<DodgePoint>();
                    while (currentNode != null) {
                        path.Insert(0, currentNode);
                        currentNode = parentMap[currentNode];
                    }
                    return path;
                }

                foreach (DodgePoint child in currentNode.Children) {
                    if (!parentMap.ContainsKey(child)) {
                        queue.Enqueue(child);
                        parentMap[child] = currentNode;
                    }
                }
            }

            return null;
        }
    }
}
