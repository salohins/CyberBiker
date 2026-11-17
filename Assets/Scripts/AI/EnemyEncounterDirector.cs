using System.Collections.Generic;
using Gameplay;
using UnityEngine;

// Encounter timing is independent of the traffic buffer. Waiting riders retain
// ordinary road navigation, with their own following slots and no combat role.
[DefaultExecutionOrder(300), DisallowMultipleComponent]
public sealed class EnemyEncounterDirector : MonoBehaviour {
    private readonly List<Enemy> riders=new List<Enemy>(10);
    private readonly Collider[] overlaps=new Collider[32];
    private readonly Plane[] cameraPlanes=new Plane[6];
    private readonly Dictionary<Enemy,float> laneShiftTimes=new Dictionary<Enemy,float>();
    private GameObject prefab,player;
    private PlayerController playerController;
    private GameplayManager gameplay;
    private DifficultyManager difficulty;
    private WorldGenerator world;
    private TrafficSpawner owner;
    private AI.AIController navigation;
    private Enemy fighter;
    private Difficulty waveSection;
    private int observedSectionRevision = -1, wavesInSection;
    private int remaining;
    private float spawnTimer,waveTimer,handoffTimer;
    private bool initialized,waveStarted,waitingForWave,arrivalChosen,rearArrival;
    private Camera gameplayCamera;
    public Enemy ActiveFighter=>fighter;
    public IReadOnlyList<Enemy> Riders=>riders;
    public int RemainingToSpawn=>remaining;
    public int WavesStarted {get;private set;}
    public int SpawnedCount {get;private set;}
    public int RearArrivalsSpawned {get;private set;}
    public int FrontArrivalsSpawned {get;private set;}
    public Vector3 LastSpawnPosition {get;private set;}
    public float WaveCooldownRemaining=>waitingForWave ? waveTimer : 0;

    public void Initialize(GameObject enemyPrefab,GameObject rider,WorldGenerator road,DifficultyManager settings,TrafficSpawner traffic=null) {
        if(initialized)return;
        prefab=enemyPrefab;player=rider;world=road;difficulty=settings;owner=traffic;
        playerController=player!=null ? player.GetComponent<PlayerController>() : null;
        gameplay=FindAnyObjectByType<GameplayManager>();
        navigation=FindAnyObjectByType<AI.AIController>();
        gameplayCamera=Camera.main;
        if(prefab==null || prefab.GetComponent<Enemy>()==null || player==null || world==null || difficulty==null)return;
        // Adopt authored scene enemies rather than adding a duplicate first foe.
        foreach(var enemy in Enemy.Instances)
            if(enemy!=null && enemy.isActiveAndEnabled && !riders.Contains(enemy))riders.Add(enemy);
        riders.Sort((a,b)=>Vector3.Distance(a.transform.position,player.transform.position).CompareTo(Vector3.Distance(b.transform.position,player.transform.position)));
        initialized=true;
        if(riders.Count>0)fighter=riders[0];
        UpdateRoles();
    }
    private void Update() {
        if(!initialized || (owner!=null && !owner.isActiveAndEnabled) || gameplay==null || gameplay.gameState!=GameState.Playing || playerController==null || !playerController.IsAlive)return;
        var settings=difficulty.currentDifficulty;if(settings==null)return;
        float dt=Time.deltaTime;if(dt<=0)return;
        if(waveSection!=settings || observedSectionRevision!=difficulty.SectionRevision) {
            waveSection=settings;observedSectionRevision=difficulty.SectionRevision;
            wavesInSection=0;remaining=0;waveStarted=false;waitingForWave=false;arrivalChosen=false;
        }
        bool lostFighter=false;
        for(int i=riders.Count-1;i>=0;i--) {
            var enemy=riders[i];
            if(enemy!=null && enemy.isActiveAndEnabled && (!enemy.HasInitialized || enemy.IsAlive))continue;
            if(enemy==fighter){fighter=null;lostFighter=true;}
            if(!ReferenceEquals(enemy,null))laneShiftTimes.Remove(enemy);
            riders.RemoveAt(i);
        }
        if(lostFighter)handoffTimer=Mathf.Max(0,settings.enemyHandoffDelay);
        if(fighter==null && riders.Count>0) {
            handoffTimer=Mathf.Max(0,handoffTimer-dt);
            if(handoffTimer<=0)foreach(var candidate in riders)
                if(candidate.HasInitialized && candidate.IsAlive && !candidate.IsArriving){fighter=candidate;break;}
        }
        UpdateRoles();
        UpdateLaneChanges(settings);
        if(difficulty.IsConstantCombat){MaintainCombatPack(settings,dt);return;}
        if(settings.enemiesPerWave<=0){remaining=0;waveStarted=false;waitingForWave=false;return;}
        if(!waveStarted) {
            if(!CanBeginWave(settings))return;
            BeginWave(settings,true);
        }
        if(waitingForWave) {
            waveTimer=Mathf.Max(0,waveTimer-dt);
            if(waveTimer>0)return;
            if(!CanBeginWave(settings))return;
            BeginWave(settings,false);
        }
        spawnTimer=Mathf.Max(0,spawnTimer-dt);
        if(remaining>0 && riders.Count<Mathf.Clamp(settings.maxEnemiesOnRoad,1,10) && spawnTimer<=0) {
            if(TrySpawn(settings)){remaining--;SpawnedCount++;spawnTimer=Mathf.Max(0,settings.enemySpawnInterval);}
            else spawnTimer=.25f; // Road/clear lane may become available next frame.
        }
        if(remaining==0 && riders.Count==0 && !waitingForWave){waitingForWave=true;waveTimer=Mathf.Max(0,settings.enemyWaveCooldown);}
    }
    private void MaintainCombatPack(Difficulty settings,float dt) {
        int target=Mathf.Clamp(settings.maxEnemiesOnRoad,1,10);
        if(!waveStarted){
            waveStarted=true;WavesStarted++;
            spawnTimer=riders.Count==0 ? Mathf.Max(0,settings.enemyInitialDelay) : 0;
        }
        waitingForWave=false;
        remaining=Mathf.Max(0,target-riders.Count);
        spawnTimer=Mathf.Max(0,spawnTimer-dt);
        if(remaining==0 || spawnTimer>0)return;
        if(TrySpawn(settings)){
            SpawnedCount++;remaining--;
            spawnTimer=Mathf.Max(0,settings.enemySpawnInterval);
        }else spawnTimer=.25f;
    }
    private bool CanBeginWave(Difficulty settings) => settings.maxWavesPerSection<=0 || wavesInSection<settings.maxWavesPerSection;
    private void BeginWave(Difficulty settings,bool first) {
        wavesInSection++;
        WavesStarted++;waveStarted=true;waitingForWave=false;
        remaining=Mathf.Max(0,Mathf.Clamp(settings.enemiesPerWave,0,10)-(first ? riders.Count : 0));
        spawnTimer=first && riders.Count==0 ? Mathf.Max(0,settings.enemyInitialDelay) : first ? Mathf.Max(0,settings.enemySpawnInterval) : 0;
    }
    private void UpdateRoles() {
        float spacing=difficulty!=null && difficulty.currentDifficulty!=null ? Mathf.Max(8,difficulty.currentDifficulty.enemyQueueSpacing) : 18;
        float distance=gameplay!=null ? gameplay.EnemyDistanceFromPlayer : 20;int slot=0;
        foreach(var enemy in riders)if(enemy!=null) {
            var settings=difficulty!=null ? difficulty.currentDifficulty : null;
            if(!enemy.HasLanePreference && settings!=null && world.TryGetLaneFrame(enemy.transform.position,out var frame) && frame.IsCurrent) {
                int count=frame.tile.carLines.Length,best=0,usage=int.MaxValue;float nearest=float.PositiveInfinity;
                for(int i=0;i<count;i++) {
                    int used=LaneUsage(i,count);float distanceToLane=Mathf.Abs(enemy.transform.position.x-(frame.origin.x+frame.right.x*Mathf.Lerp(frame.min,frame.max,i/(float)Mathf.Max(1,count-1))));
                    if(used<usage || (used==usage && distanceToLane<nearest)){best=i;usage=used;nearest=distanceToLane;}
                }
                enemy.SetRidingLane(best/(float)Mathf.Max(1,count-1),settings);
            } else if(enemy.HasLanePreference && settings!=null)enemy.SetRidingLane(enemy.LanePreference,settings);
            bool queued=enemy!=fighter;
            enemy.SetEncounterRole(queued,distance+(queued ? ++slot*spacing : 0));
        }
    }
    private int LaneUsage(int lane,int count) {
        int used=0;foreach(var enemy in riders)if(enemy!=null && enemy.HasLanePreference && Mathf.RoundToInt(enemy.LanePreference*(count-1))==lane)used++;
        return used;
    }
    private static int LaneRank(Transform lane,RoadLaneFrame frame) {
        int rank=0;float lateral=Vector3.Dot(lane.position-frame.origin,frame.right);
        foreach(var other in frame.tile.carLines)if(other!=null && Vector3.Dot(other.position-frame.origin,frame.right)<lateral-.01f)rank++;
        return rank;
    }
    private void UpdateLaneChanges(Difficulty settings) {
        float interval=Mathf.Max(1,settings.enemyLaneChangeInterval);
        foreach(var enemy in riders) {
            if(enemy==null || !enemy.HasLanePreference || !enemy.HasInitialized || !enemy.IsAlive)continue;
            if(!laneShiftTimes.TryGetValue(enemy,out float next)) {
                laneShiftTimes[enemy]=Time.time+interval*(.7f+Mathf.Repeat(enemy.GetEntityId().GetHashCode()*.137f,.6f));continue;
            }
            if(Time.time<next || enemy.IsArriving || enemy.IsFinisherReady)continue;
            laneShiftTimes[enemy]=Time.time+interval*(.85f+Random.value*.3f);
            if(!world.TryGetLaneFrame(enemy.transform.position,out var frame) || !frame.IsCurrent)continue;
            int count=frame.tile.carLines.Length;if(count<2)continue;
            int current=Mathf.RoundToInt(enemy.LanePreference*(count-1));
            int destination=current==0 ? 1 : current==count-1 ? count-2 : current+(Random.value<.5f ? -1 : 1);
            if(LaneUsage(current,count)>LaneUsage(destination,count))enemy.SetRidingLane(destination/(float)(count-1),settings);
            else foreach(var partner in riders) {
                if(partner==null || partner==enemy || partner.IsArriving || partner.IsFinisherReady || !partner.IsAlive || !partner.HasLanePreference)continue;
                if(Mathf.RoundToInt(partner.LanePreference*(count-1))!=destination || Mathf.Abs(partner.transform.position.z-enemy.transform.position.z)<12)continue;
                partner.SetRidingLane(current/(float)(count-1),settings);enemy.SetRidingLane(destination/(float)(count-1),settings);
                laneShiftTimes[partner]=Time.time+interval;break;
            }
        }
    }
    private bool TrySpawn(Difficulty settings) {
        if(!arrivalChosen){rearArrival=Random.value*100<Mathf.Clamp(settings.enemyRearArrivalPercent,0,100);arrivalChosen=true;}
        float spacing=Mathf.Max(8,settings.enemyQueueSpacing);
        float fightingDistance=gameplay!=null ? gameplay.EnemyDistanceFromPlayer : 20;
        // Keep new arrivals beyond the fighting slot even when the global gap
        // is tuned farther out than the requested introduction distance.
        float distance=Mathf.Max(100,Mathf.Max(settings.enemySpawnDistance,fightingDistance+spacing))+Mathf.Max(0,riders.Count-1)*spacing;
        foreach(var rider in riders)if(rider!=null && !rider.IsArriving)
            distance=Mathf.Max(distance,rider.transform.position.z-player.transform.position.z+spacing);
        if(rearArrival)distance=-Mathf.Max(Mathf.Max(20,settings.enemyRearSpawnDistance),gameplayCamera!=null ? player.transform.position.z-gameplayCamera.transform.position.z+15 : 20);
        var desired=player.transform.position+Vector3.forward*distance;
        if(!world.TryGetLaneFrame(desired,out var frame) || !frame.IsCurrent || !frame.ContainsForward(desired))return false;
        var lanes=frame.tile.carLines;if(lanes==null || lanes.Length==0)return false;
        int first=Random.Range(0,lanes.Length);
        int best=-1,bestUsage=int.MaxValue;Vector3 bestPosition=default;
        if(gameplayCamera!=null)GeometryUtility.CalculateFrustumPlanes(gameplayCamera,cameraPlanes);
        for(int attempt=0;attempt<lanes.Length;attempt++) {
            var lane=lanes[(first+attempt)%lanes.Length];if(lane==null)continue;
            var position=lane.position+frame.forward*(frame.ForwardOffset(desired)-frame.ForwardOffset(lane.position));
            if(rearArrival) {
                if(Mathf.Abs(position.x-player.transform.position.x)<2.5f)continue;
                if(gameplayCamera!=null && GeometryUtility.TestPlanesAABB(cameraPlanes,new Bounds(position+Vector3.up*1.5f,new Vector3(4,4,6))))continue;
            }
            int count=Physics.OverlapCapsuleNonAlloc(position+Vector3.up*.85f,position+Vector3.up*2.6f,.7f,overlaps,~0,QueryTriggerInteraction.Ignore);
            bool blocked=count==overlaps.Length;
            for(int i=0;i<count && !blocked;i++)
                blocked=overlaps[i].GetComponentInParent<Car>()!=null || overlaps[i].GetComponentInParent<Obstacle>()!=null || overlaps[i].GetComponentInParent<Enemy>()!=null || overlaps[i].CompareTag("Wall");
            if(blocked)continue;
            // An empty spawn position can still lie outside every legal
            // turn-angle connection to the generated traffic network.
            if(navigation!=null && !navigation.CanEnterTraffic(position))continue;
            int used=LaneUsage(LaneRank(lane,frame),lanes.Length);
            if(used<bestUsage){best=first+attempt;bestPosition=position;bestUsage=used;}
        }
        if(best>=0) {
            var chosen=lanes[best%lanes.Length];
            var instance=Instantiate(prefab,bestPosition,Quaternion.LookRotation(frame.forward));var enemy=instance.GetComponent<Enemy>();
            enemy.SetRidingLane(LaneRank(chosen,frame)/(float)Mathf.Max(1,lanes.Length-1),settings);
            enemy.BeginArrival(player,rearArrival,settings.enemyArrivalClosingSpeed,settings.enemyArrivalFadeTime);
            LastSpawnPosition=bestPosition;
            riders.Add(enemy);
            if(rearArrival)RearArrivalsSpawned++;else FrontArrivalsSpawned++;
            arrivalChosen=false;
            UpdateRoles();return true;
        }
        return false;
    }
}
