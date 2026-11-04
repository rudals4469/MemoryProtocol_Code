using UnityEngine;
namespace MemoryProtocol
{
 [CreateAssetMenu(menuName="MemoryProtocol/Maze movement settings")]
 public sealed class MazeMovementSettings:ScriptableObject
 {
  [Min(.1f)]public float cellsPerSecond=5.5f;
  [Range(.05f,.45f)]public float turnTolerance=.25f;
  [Range(.01f,.4f)]public float goalTolerance=.2f;
  [Header("한 칸 이동 공통 조작")]
  [Min(.1f)]public float stepRepeatDelay=.16f;
  [Min(.05f)]public float stepRepeatInterval=.16f;
  [Min(.01f)]public float stepMoveDuration=.16f;
 }
 public interface IMazePath
 {
  bool TryStep(Vector2Int cell,Vector2Int direction,out Vector2Int next);
  bool HasPort(Vector2Int cell,Vector2Int direction);
  bool CanOccupy(Vector2Int cell);
  Vector3 CellWorldPosition(Vector2Int cell);
  Vector3 OffsetWorldVector(Vector2Int cell,Vector2 offset);
  float EdgeLimit(Vector2Int cell,Vector2Int direction);
 }
 public interface IMazeEdgeDirection
 {
  Vector2Int DirectionBetween(Vector2Int from,Vector2Int to);
 }
 // All maze mechanics supply topology and geometry; movement behavior lives here only.
 public sealed class MazePathMotor
 {
  private IMazePath path;
  private MazeMovementSettings settings;
  public Vector2Int Current{get;private set;}
  public Vector2Int Next{get;private set;}
  // Resource mechanics update their state before the next edge is evaluated.
  public event System.Action<Vector2Int> CellEntered;
  private float progress;
  private Vector2 offset;
  private Vector2Int travelDirection;
  private readonly MazeStepInput cellInput=new MazeStepInput();
  private Vector2Int cellDirection;
  private float cellTime,cellDuration;
  private System.Action afterCell;
  public bool IsStepping{get;private set;}
  private readonly System.Collections.Generic.HashSet<Vector2Int> passedCells=new System.Collections.Generic.HashSet<Vector2Int>();
  public void Initialize(IMazePath topology,MazeMovementSettings profile,Vector2 point){path=topology;settings=profile;Rebase(point);}
  public void Rebase(Vector2 point){Current=Next=Vector2Int.RoundToInt(point);progress=0;offset=point-Current;travelDirection=Vector2Int.zero;passedCells.Clear();cellInput.Reset();IsStepping=false;cellTime=cellDuration=0;afterCell=null;}
  private static Vector2Int Direction(Vector2 delta)=>new Vector2Int(System.Math.Sign(delta.x),System.Math.Sign(delta.y));
  private Vector2Int EdgeDirection()=>path is IMazeEdgeDirection edges?edges.DirectionBetween(Current,Next):Direction(Next-Current);
  public Vector2 SourcePoint
  {
   get
   {
    if(Current==Next)return (Vector2)Current+offset;
    var direction=EdgeDirection();
    return progress<=.5f?(Vector2)Current+(Vector2)direction*progress:(Vector2)Next-(Vector2)direction*(1-progress);
   }
  }
  public Vector3 WorldPosition=>Current==Next?path.CellWorldPosition(Current)+path.OffsetWorldVector(Current,offset):Vector3.Lerp(path.CellWorldPosition(Current),path.CellWorldPosition(Next),progress);
  public bool Visible=>path!=null&&path.CanOccupy(Current)&&(Current==Next||path.CanOccupy(Next)&&path.TryStep(Current,EdgeDirection(),out var available)&&available==Next);
  public bool Reached(Vector2Int goal)=>passedCells.Contains(goal)||(!IsStepping||Current==Next)&&Visible&&Vector2.Distance(SourcePoint,goal)<(settings!=null?settings.goalTolerance:.2f);
  private float RepeatDelay=>settings!=null?settings.stepRepeatDelay:.16f;
  private float RepeatInterval=>settings!=null?settings.stepRepeatInterval:.16f;
  public void PressStep(Vector2Int direction)
  {
   if(cellInput.Press(direction,IsStepping,RepeatDelay))BeginCell(direction);
  }
  public void AfterStep(System.Action interaction)
  {
   if(IsStepping)afterCell=interaction;else interaction();
  }
  private void BeginCell(Vector2Int direction)
  {
   if(offset.sqrMagnitude<.000001f)offset=Vector2.zero;
   if(!Visible||Current!=Next||offset!=Vector2.zero||!path.TryStep(Current,direction,out _))return;
   cellDirection=direction;cellTime=0;cellDuration=Mathf.Max(.01f,settings!=null?settings.stepMoveDuration:.16f);IsStepping=true;
  }
  // 게임 입력은 공통 한 칸 이동. 연속 Move는 중력·미끄러짐 및 검증용으로 유지한다.
  public void AdvanceSteps(Vector2Int direction,float deltaTime)
  {
   float elapsed=Mathf.Max(0,deltaTime);
   passedCells.Clear();
   float availableAfterMove=elapsed;
   if(IsStepping)availableAfterMove=AdvanceCell(elapsed);
   if(!IsStepping&&afterCell!=null)
   {
    var interaction=afterCell;afterCell=null;cellInput.Reset();interaction();return;
   }
   var next=cellInput.Advance(direction,elapsed,IsStepping,RepeatDelay,RepeatInterval,availableAfterMove);
   if(next!=Vector2Int.zero)
   {
    BeginCell(next);
    if(IsStepping&&cellInput.NextStepElapsed>0)AdvanceCell(cellInput.NextStepElapsed);
   }
  }
  private float AdvanceCell(float elapsed)
  {
   float advance=Mathf.Min(elapsed,Mathf.Max(0,cellDuration-cellTime));cellTime+=advance;
   Move(cellDirection,advance/(cellDuration*(settings!=null?settings.cellsPerSecond:5.5f)),false,false);
   if(cellTime>=cellDuration-.00001f)IsStepping=false;
   return Mathf.Max(0,elapsed-advance);
  }
  // Cell-based mechanics share topology and goal detection with continuous mazes.
  public void Step(Vector2Int direction)
  {
   passedCells.Clear();
   if(!Visible||Current!=Next||offset!=Vector2.zero||!path.TryStep(Current,direction,out _))return;
   Move(direction,1f/(settings!=null?settings.cellsPerSecond:5.5f),false);
  }
  public void Move(Vector2Int direction,float deltaTime,bool continueForwardWhenBlocked=true,bool clearPassedCells=true)
  {
   if(clearPassedCells)passedCells.Clear();
   if(!Visible||Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1)return;
   float remaining=(settings!=null?settings.cellsPerSecond:5.5f)*deltaTime;
   float tolerance=settings!=null?settings.turnTolerance:.25f;
   while(remaining>.0001f)
   {
    if(Current==Next)
    {
     if(offset!=Vector2.zero)
     {
      var outward=Direction(offset);
      if(direction==outward)
      {
       if(path.TryStep(Current,direction,out var joined)){Next=joined;progress=offset.magnitude;offset=Vector2.zero;}
       else{ToEdge(direction,remaining);break;}
      }
      else
      {
       float back=Mathf.Min(remaining,offset.magnitude);offset=Vector2.MoveTowards(offset,Vector2.zero,back);remaining-=back;
       if(offset!=Vector2.zero)break;
      }
     }
     if(Current==Next)
     {
      if(!path.TryStep(Current,direction,out var next))
      {
       if(path.HasPort(Current,direction)){ToEdge(direction,remaining);break;}
       if(!continueForwardWhenBlocked||travelDirection==Vector2Int.zero||!path.TryStep(Current,travelDirection,out next))break;
      }
      Next=next;
     }
    }
    else
    {
     var moving=EdgeDirection();
     if(!path.TryStep(Current,moving,out var available)||available!=Next)break;
     if(direction==-moving){var swap=Current;Current=Next;Next=swap;progress=1-progress;}
     else if(direction!=moving&&progress<tolerance&&path.TryStep(Current,direction,out _))
     {
      float back=Mathf.Min(remaining,progress);progress-=back;remaining-=back;
      if(progress>0)break;Next=Current;continue;
     }
    }
    travelDirection=EdgeDirection();
    float advance=Mathf.Min(remaining,1-progress);progress+=advance;remaining-=advance;
    if(progress>=.99999f){Current=Next;progress=0;passedCells.Add(Current);CellEntered?.Invoke(Current);}
    else break;
   }
  }
  private void ToEdge(Vector2Int direction,float amount)
  {
   if(!path.HasPort(Current,direction))return;
   offset=Vector2.MoveTowards(offset,(Vector2)direction*path.EdgeLimit(Current,direction),amount);
  }
 }
}
