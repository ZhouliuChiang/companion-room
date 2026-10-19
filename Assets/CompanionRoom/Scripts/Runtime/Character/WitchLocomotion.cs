using System;
using System.Collections.Generic;
using UnityEngine;
namespace CompanionRoom
{
    /// <summary>
    /// Click-to-walk for the witch. A conservative circular footprint (radius covers skirt, hands and
    /// hat brim at every yaw, so turning in place can never clip) is tested against every collider in
    /// the room; routes are found on a grid, then straightened wherever the straight line is clear.
    /// She eases in and out of the walk, turns while walking, and turns to face the camera when she
    /// arrives. A click on a spot she cannot stand on snaps to the nearest free spot close by; if
    /// there is none, or no route, she stays put.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class WitchLocomotion : MonoBehaviour
    {
        public Bounds roomBounds;
        public float floor = .468f, radius = .53f, height = 1.35f, speed = .38f;
        [Tooltip("Footprint below the hat brim: skirt, swinging hands and skirt flare (m).")]
        public float bodyRadius = .37f;
        [Tooltip("Height above the floor where the hat brim starts; above it the full radius applies.")]
        public float brimHeight = .72f;
        [Tooltip("How far a blocked click may be moved to the nearest spot she can stand on (m).")]
        public float snapDistance = .35f;
        const float Cell = .15f, TurnSpeed = 260f, Accel = .9f, Decel = .7f;
        readonly List<Vector3> route = new();
        readonly Collider[] overlaps = new Collider[256];
        Animator animator;
        RoomCompanion companion;
        float velocity;
        bool walking;
        int nx, nz;
        Vector3 origin;
        public bool IsWalking => route.Count > 0;
        /// <summary>Raised for every GoTo: the spot she will walk to (or the refused spot) and whether she goes.</summary>
        public event Action<Vector3, bool> DestinationChosen;
        void Awake() { animator = GetComponentInChildren<Animator>(); companion = GetComponent<RoomCompanion>(); }
        public bool Clear(Vector3 p)
        {
            float body=Mathf.Min(bodyRadius,radius);
            if(p.x < roomBounds.min.x+body || p.x > roomBounds.max.x-body || p.z < roomBounds.min.z+body || p.z > roomBounds.max.z-body) return false;
            // Two stacked volumes: the narrow body up to the brim, the wide hat brim above it.
            // Include interaction colliders: their furniture still blocks movement.
            return Free(Physics.OverlapCapsuleNonAlloc(p+Vector3.up*(.035f+body),p+Vector3.up*Mathf.Max(.035f+body,brimHeight-body),body,overlaps,~0,QueryTriggerInteraction.Collide),null)
                && BrimFree(p) && Supported(p,body);
        }
        // The brim band is wider than it is tall, so it is an octagonal prism (two boxes, 45° apart):
        // a collider blocks only if it touches both boxes.
        readonly Collider[] brimOverlaps = new Collider[256];
        readonly HashSet<Collider> brimSet = new();
        bool BrimFree(Vector3 p)
        {
            float half=(height-brimHeight)*.5f;
            var center=p+Vector3.up*(brimHeight+half);var extents=new Vector3(radius,half,radius);
            int n=Physics.OverlapBoxNonAlloc(center,extents,brimOverlaps,Quaternion.identity,~0,QueryTriggerInteraction.Collide);
            if(n==brimOverlaps.Length)return false;
            brimSet.Clear();for(int i=0;i<n;i++)brimSet.Add(brimOverlaps[i]);
            return Free(Physics.OverlapBoxNonAlloc(center,extents,overlaps,Quaternion.Euler(0,45,0),~0,QueryTriggerInteraction.Collide),brimSet);
        }
        bool Free(int n,HashSet<Collider> alsoIn)
        {
            if(n==overlaps.Length)return false;
            for(int i=0;i<n;i++)
            {
                var c=overlaps[i]; if(c.transform.IsChildOf(transform))continue;
                if(alsoIn!=null&&!alsoIn.Contains(c))continue;
                var prop=c.GetComponentInParent<ImportedRoomAsset>();
                if(prop!=null && prop.assetId=="Rug")continue;
                // Floor is below the footprint; wall and furniture intersections are rejected.
                if(c.bounds.max.y<=floor+.045f)continue;
                return false;
            }
            return true;
        }
        /// <summary>Why a spot is refused: ' ' free, 'W' room edge, 'B' body hits c, 'H' hat brim hits c, 'S' no floor.</summary>
        public char Diagnose(Vector3 p,out Collider blocker)
        {
            blocker=null;float body=Mathf.Min(bodyRadius,radius);
            if(p.x < roomBounds.min.x+body || p.x > roomBounds.max.x-body || p.z < roomBounds.min.z+body || p.z > roomBounds.max.z-body) return 'W';
            int n=Physics.OverlapCapsuleNonAlloc(p+Vector3.up*(.035f+body),p+Vector3.up*Mathf.Max(.035f+body,brimHeight-body),body,overlaps,~0,QueryTriggerInteraction.Collide);
            if(!Free(n,null)){blocker=First(n,null);return 'B';}
            if(!BrimFree(p)){blocker=First(Physics.OverlapBoxNonAlloc(p+Vector3.up*(brimHeight+(height-brimHeight)*.5f),new Vector3(radius,(height-brimHeight)*.5f,radius),overlaps,Quaternion.Euler(0,45,0),~0,QueryTriggerInteraction.Collide),brimSet);return 'H';}
            return Supported(p,body)?' ':'S';
        }
        Collider First(int n,HashSet<Collider> alsoIn)
        {
            for(int i=0;i<n;i++){var c=overlaps[i];if(c.transform.IsChildOf(transform)||(alsoIn!=null&&!alsoIn.Contains(c)))continue;var prop=c.GetComponentInParent<ImportedRoomAsset>();if(prop!=null&&prop.assetId=="Rug")continue;if(c.bounds.max.y<=floor+.045f)continue;return c;}
            return null;
        }
        bool Supported(Vector3 p,float r)
        {
            // Verify real floor support, including at the outer edge of the footprint.
            foreach(var offset in new[]{Vector3.zero,Vector3.right*r,Vector3.left*r,Vector3.forward*r,Vector3.back*r})
            {
                bool support=false;
                foreach(var h in Physics.RaycastAll(p+offset+Vector3.up*.12f,Vector3.down,.2f,~0,QueryTriggerInteraction.Ignore))
                    if(!h.transform.IsChildOf(transform)&&h.normal.y>.9f&&Mathf.Abs(h.point.y-floor)<.04f)support=true;
                if(!support)return false;
            }
            return true;
        }
        public bool GoTo(Vector3 point)
        {
            bool ok=Plan(point,out var destination);
            DestinationChosen?.Invoke(destination,ok);
            return ok;
        }
        bool Plan(Vector3 point,out Vector3 destination)
        {
            point.y=floor; destination=point; Physics.SyncTransforms();
            if(!Clear(point)){ if(!Nearest(point,out point))return false; destination=point; }
            float body=Mathf.Min(bodyRadius,radius);
            origin=new Vector3(roomBounds.min.x+body,floor,roomBounds.min.z+body);
            nx=Mathf.Max(1,Mathf.FloorToInt((roomBounds.size.x-2*body)/Cell)+1);nz=Mathf.Max(1,Mathf.FloorToInt((roomBounds.size.z-2*body)/Cell)+1);
            int Index(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt((p.z-origin.z)/Cell),0,nz-1)*nx+Mathf.Clamp(Mathf.RoundToInt((p.x-origin.x)/Cell),0,nx-1);
            Vector3 Pos(int i)=>origin+new Vector3(i%nx*Cell,0,i/nx*Cell);
            var cell=new sbyte[nx*nz]; // 0 unknown, 1 clear, -1 blocked (each cell tested once)
            bool CellClear(int i){ if(cell[i]==0)cell[i]=(sbyte)(Clear(Pos(i))?1:-1); return cell[i]>0; }
            var here=transform.position; here.y=floor;
            int start=Entry(Index(here),here),end=Entry(Index(point),point);
            int Entry(int c,Vector3 p)
            {
                // The grid cell nearest p that is free and reachable from p in a straight line.
                int cx=c%nx,cz=c/nx,best=-1;float bestD=float.MaxValue;
                for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                {
                    int x=cx+dx,z=cz+dz;if(x<0||z<0||x>=nx||z>=nz)continue;int i=z*nx+x;
                    float d=(Pos(i)-p).sqrMagnitude;if(d<bestD&&CellClear(i)&&SegmentClear(p,Pos(i))){bestD=d;best=i;}
                }
                return best;
            }
            if(start<0||end<0)return false;
            var previous=new int[nx*nz];var seen=new bool[nx*nz];var queue=new Queue<int>();queue.Enqueue(start);seen[start]=true;previous[start]=-1;
            int[] steps={-1,1,-nx,nx};
            while(queue.Count>0&&!seen[end])
            {
                int v=queue.Dequeue();foreach(int d in steps)
                {
                    int t=v+d;if(t<0||t>=seen.Length||seen[t]||(Mathf.Abs(d)==1&&v/nx!=t/nx))continue;
                    // Neighbouring cells are 15 cm apart; both ends plus the midpoint cover the edge.
                    if(!CellClear(t)||!Clear((Pos(v)+Pos(t))*.5f))continue;
                    seen[t]=true;previous[t]=v;queue.Enqueue(t);
                }
            }
            if(!seen[end])return false;
            var path=new List<Vector3>{here};for(int i=end,k=path.Count;i>=0;i=previous[i])path.Insert(k,Pos(i));path.Add(point);
            // Straighten: from each corner jump to the farthest point still in clear view.
            var smooth=new List<Vector3>();
            for(int i=0;i<path.Count-1;)
            {
                int j=path.Count-1;
                while(j>i+1&&!SegmentClear(path[i],path[j]))j--;
                smooth.Add(path[j]);i=j;
            }
            route.Clear();route.AddRange(smooth);return true;
        }
        bool Nearest(Vector3 point,out Vector3 found)
        {
            for(float r=.05f;r<=snapDistance+1e-4f;r+=.05f)
            {
                int count=Mathf.Max(8,Mathf.CeilToInt(2*Mathf.PI*r/.05f));
                float best=float.MaxValue;found=point;bool any=false;
                var here=transform.position;
                for(int i=0;i<count;i++)
                {
                    float a=i*2*Mathf.PI/count;var p=point+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*r;
                    // Among equally close spots prefer the one nearer to her.
                    float d=(p-here).sqrMagnitude;if(d<best&&Clear(p)){best=d;found=p;any=true;}
                }
                if(any)return true;
            }
            found=point;return false;
        }
        bool SegmentClear(Vector3 a,Vector3 b)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/.04f));
            for(int i=0;i<=steps;i++)if(!Clear(Vector3.Lerp(a,b,(float)i/steps)))return false;
            return true;
        }
        public void Stop() {route.Clear();velocity=0;}
        void Update()
        {
            var app=CompanionRoomApp.Instance;
            if(app!=null&&(app.IsBusy||(app.UI!=null&&app.UI.AnyWindowOpen))&&route.Count>0){Stop();companion?.FaceRest();}
            while(route.Count>0)
            {
                var delta=route[0]-transform.position;delta.y=0;
                if(delta.magnitude>=.01f)break;
                route.RemoveAt(0);
                if(route.Count==0){velocity=0;companion?.FaceRest();}
            }
            if(route.Count>0)
            {
                var delta=route[0]-transform.position;delta.y=0;
                float remaining=delta.magnitude;
                for(int i=1;i<route.Count;i++)remaining+=Vector3.Distance(route[i-1],route[i]);
                var facing=Quaternion.LookRotation(delta);
                transform.rotation=Quaternion.RotateTowards(transform.rotation,facing,TurnSpeed*Time.deltaTime);
                // Slow down for sharp turns and ease into the stop.
                float angle=Quaternion.Angle(transform.rotation,facing);
                float turn=Mathf.Clamp01((75f-angle)/55f);
                float target=Mathf.Max(.06f,Mathf.Min(speed,Mathf.Sqrt(2*Decel*remaining)))*turn;
                velocity=Mathf.MoveTowards(velocity,target,Time.deltaTime*(target>velocity?Accel:Decel*2));
                var next=Vector3.MoveTowards(transform.position,route[0],velocity*Time.deltaTime);next.y=floor;
                if(SegmentClear(transform.position,next))transform.position=next;else{Stop();companion?.FaceRest();}
            }
            bool move=route.Count>0;
            if(animator!=null){if(move!=walking)animator.CrossFadeInFixedTime(move?"Walk":"Idle",.25f);animator.speed=move?Mathf.Clamp(velocity/speed,.45f,1):1;}
            walking=move;
        }
    }
}
