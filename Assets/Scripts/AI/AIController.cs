using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CyberBiker.Pooling;

namespace AI {
    // Represents a point that an AI can dodge to, tracking its position relative to an offset object
    public class DodgePoint {
        public Vector3 offset { get; private set; }  // The offset from the original position, used for calculations
        public List<DodgePoint> Children { get; private set; }  // Points that this point can connect to for dodging
        public GameObject offsetObject { get; private set; }  // The object that the offset is calculated from

        private Vector3 originalPosition;
        private PooledInstance lease;
        private uint generation;
        public bool IsValid => offsetObject!=null && (lease==null || (lease.IsRented && lease.Generation==generation));

        // Constructor to create a DodgePoint with its position and related object
        public DodgePoint(Vector3 position, GameObject offsetObject) {
            Children = new List<DodgePoint>();
            Reset(position,offsetObject);
        }
        public void Reset(Vector3 position,GameObject owner){if(owner!=offsetObject)lease=owner!=null ? owner.GetComponent<PooledInstance>() : null;offsetObject=owner;offset=owner!=null ? owner.transform.position-position : Vector3.zero;originalPosition=position;generation=lease!=null ? lease.Generation : 0;Children.Clear();}

        // Returns the current position of the DodgePoint
        public Vector3 GetPosition() {
            return !IsValid ? Vector3.zero : offsetObject.CompareTag("Enemy") ? originalPosition : offsetObject.transform.position - offset;
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
        private static int waterMask,worldMask;
        private static bool masksReady;
        private static void CacheMasks(){if(masksReady)return;waterMask=LayerMask.GetMask(WaterLayer);worldMask=LayerMask.GetMask(WorldObjectLayer);masksReady=true;}

        // Returns the car lines associated with the tile at the specified position
        public static Transform[] GetLines(Vector3 position) {
            CacheMasks();
            RaycastHit hit;

            if (Physics.Raycast(new Ray(position, Vector3.down), out hit, Mathf.Infinity, waterMask)) {
                Tile tileComponent = hit.transform.gameObject.GetComponent<Tile>();
                return tileComponent?.carLines;
            }

            Debug.DrawRay(position, Vector3.up, Color.yellow);

            Debug.LogWarning("Tile component not found on the hit object.");
            return null;
        }

        // Returns any colliders that overlap with the given position, within the specified pass length
        public static Collider[] GetOverlay(Vector3 position, float passLength) {
            CacheMasks();return Physics.OverlapBox(position, new Vector3(1, 10f, passLength / 2), Quaternion.identity, worldMask);
        }
    }

    public class AIController : MonoBehaviour {
        [SerializeField] private bool showPointRelations;  // Whether to show lines connecting related dodge points
        [Range(0, 90)]
        [SerializeField] public float dodgeAngleLimit;  // The maximum angle that an AI can dodge
        [SerializeField] private float collisionRadius;  // The radius used for collision detection between points

        [System.NonSerialized] public List<DodgePoint> dodgePoints;  // A list of all dodge points currently tracked by the AI
        private List<GameObject> activeCars;  // A list of all active cars in the scene
        private int worldMask;
        public float NavigationRadius => collisionRadius;

        // Initialize the dodge points and active cars lists
        private void Awake() {
            dodgePoints = new List<DodgePoint>();
            activeCars = new List<GameObject>();
            worldMask=LayerMask.GetMask("WorldObject");
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
            if(dodgePoints.Count==0)return null;
            var candidates=CollectReachable(rootNode);
            return candidates.Count>0 ? candidates[Random.Range(0,candidates.Count)] : null;
        }
        // A rider-local road point fills empty stretches between traffic rows.
        // Existing car points retain priority as soon as a detour is nearby.
        // The original public query and its random selection remain unchanged.
        public DodgePoint GetPath(DodgePoint rootNode,DodgePoint roadPoint) {
            var candidates=CollectReachable(rootNode);
            if(roadPoint!=null && roadPoint.IsValid && CanReach(rootNode.GetPosition(),roadPoint.GetPosition())) {
                bool trafficNear=false;float end=roadPoint.GetPosition().z+10;
                foreach(var candidate in candidates)if(candidate.GetPosition().z<=end){trafficNear=true;break;}
                // Car spacing guarantees connections between car-generated
                // points, not arbitrary lane positions in between them. Keep
                // at least one legal onward connection before inserting one.
                bool onward=false;
                foreach(var candidate in candidates)if(CanReach(roadPoint.GetPosition(),candidate.GetPosition())){onward=true;break;}
                bool trafficAhead=false;
                if(candidates.Count==0)foreach(var point in dodgePoints)if(point.IsValid && point.GetPosition().z>rootNode.GetPosition().z){trafficAhead=true;break;}
                if(!trafficNear && (onward || (candidates.Count==0 && !trafficAhead)))return roadPoint;
            }
            return candidates.Count>0 ? candidates[Random.Range(0,candidates.Count)] : null;
        }
        public bool CanEnterTraffic(Vector3 origin) {
            bool trafficAhead=false;
            foreach(var point in dodgePoints) {
                if(!point.IsValid)continue;var position=point.GetPosition();if(position.z<=origin.z)continue;
                trafficAhead=true;if(CanReach(origin,position))return true;
            }
            return !trafficAhead;
        }
        private bool CanReach(Vector3 origin,Vector3 position) => position.z>origin.z &&
            Mathf.Abs(Vector3.Angle(position-origin,Vector3.forward))<dodgeAngleLimit &&
            !Physics.CheckCapsule(origin,position,collisionRadius,worldMask);
        private List<DodgePoint> CollectReachable(DodgePoint rootNode) {
            var origin=rootNode.GetPosition();var candidates=rootNode.Children;candidates.Clear();
            // Preserve candidate order, angle tests, capsule radius and random
            // selection. Cache each moving point once; do not shift the global list.
            for(int i=0;i<dodgePoints.Count;i++){
                var point=dodgePoints[i];if(!point.IsValid)continue;var position=point.GetPosition();
                if(CanReach(origin,position))candidates.Add(point);
            }
            return candidates;
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
