using System;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryProtocol
{
    public sealed partial class AssemblyMazePuzzle : MonoBehaviour, ISharedPuzzleInput, IPressedPuzzleInput, IMazePath, IMazeEdgeDirection
    {
        public PuzzleWindow window;
        public SharedPuzzleControls controls;
        public RectTransform movementArea;
        public RectTransform[] pieces = Array.Empty<RectTransform>();
        public RectTransform[] faces = Array.Empty<RectTransform>();
        public Vector2Int[] slots = Array.Empty<Vector2Int>();
        public MazeMovementSettings movementSettings;
        public Vector2Int pieceDimensions = new Vector2Int(4, 4);
        public Vector2Int entrance, exit;
        public Vector2Int[] floorCells = Array.Empty<Vector2Int>();
        public Image playerMarker;
        [Min(0)] public float snapDistance = 22;
        [Min(0)] public float snapPulseDuration = .18f;
        public Image[] joinFeedback = Array.Empty<Image>();
        readonly MazePathMotor motor = new MazePathMotor();
        Vector2[] initialPositions;
        int[] groups;
        float pulse;
        public bool IsAssembled
        {
            get { if (UsesSocketBoard) return AllSocketsFilled; if (groups == null || groups.Length != 4) return false; for (int i = 1; i < groups.Length; i++) if (groups[i] != groups[0]) return false; return FitsSquare(groups[0], -1, Vector2.zero); }
        }
        public Vector2 SourcePoint => motor.SourcePoint;
        public int SelectedPiece { get; private set; }
        public bool SameGroup(int a, int b) => UsesSocketBoard ? a == b : groups[a] == groups[b];
        public bool IsPieceSelected(int index) => groups != null && index >= 0 && index < groups.Length && SameGroup(SelectedPiece,index);
        public void SelectPiece(int index)
        {
            if(window.IsSolved) return;
            if(index < -1 || index >= pieces.Length) return;
            SelectedPiece=index; controls.Select(this);
        }
        void RefreshSelectionHighlight() => GetComponent<PuzzleSelectionHighlight>()?.RefreshSelection();
        void Awake() => CapturePositions();
        void CapturePositions()
        {
            if (initialPositions != null) return;
            initialPositions = new Vector2[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) initialPositions[i] = pieces[i].anchoredPosition;
        }
        void OnEnable() { if (pieces.Length == 4 && playerMarker != null) InitializeAuthoredView(); }
        public void InitializeAuthoredView()
        {
            if (UsesSocketBoard) { InitializeSocketBoard(); return; }
            CapturePositions(); groups = new int[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) { groups[i] = i; pieces[i].anchoredPosition = initialPositions[i]; if(pieces[i].parent!=transform)pieces[i].gameObject.SetActive(true); }
            SelectedPiece=0; pulse = 0; foreach (var image in joinFeedback) image.enabled = false;
            motor.Initialize(this, movementSettings, entrance); SyncPresentation(); PlaceMarker();
            RefreshSelectionHighlight();
        }
        // Stage fragments may be authored between other puzzle windows in the same movement area.
        public void SyncPresentation()
        {
            var owner=GetComponent<CanvasGroup>();if(owner==null)return;
            foreach(var piece in pieces)if(piece!=null&&piece.parent!=transform&&piece.TryGetComponent<CanvasGroup>(out var group))
            { group.alpha=owner.alpha;group.blocksRaycasts=owner.blocksRaycasts;group.interactable=owner.interactable; }
        }
        void OnDisable() { foreach(var piece in pieces)if(piece!=null&&piece.parent!=transform)piece.gameObject.SetActive(false); }
        static readonly Vector2Int[] Directions = {Vector2Int.right, Vector2Int.down, Vector2Int.left, Vector2Int.up};
        Vector2 Pitch => pieces[0].rect.size;
        bool FitsSquare(int moving, int target, Vector2 delta)
        {
            Vector2 min = new Vector2(float.PositiveInfinity,float.PositiveInfinity), max = -min;
            for(int i=0;i<pieces.Length;i++) if(groups[i]==moving || groups[i]==target)
            {
                var pos=pieces[i].anchoredPosition+(groups[i]==moving?delta:Vector2.zero);
                min=Vector2.Min(min,pos);max=Vector2.Max(max,pos);
                for(int j=0;j<i;j++)if(groups[j]==moving||groups[j]==target)
                {
                    var other=pieces[j].anchoredPosition+(groups[j]==moving?delta:Vector2.zero);
                    if(Mathf.Abs(pos.x-other.x)<Pitch.x-.01f&&Mathf.Abs(pos.y-other.y)<Pitch.y-.01f)return false;
                }
            }
            return max.x-min.x<=Pitch.x+.01f && max.y-min.y<=Pitch.y+.01f;
        }
        // Detaching a bridge can split the remaining fragments into separate connected groups.
        public void Detach(int index)
        {
            if(window.IsSolved)return;
            if (UsesSocketBoard) { DetachSocket(index); return; }
            var previous=(int[])groups.Clone();
            for(int i=0;i<groups.Length;i++)groups[i]=-1;
            groups[index]=index;
            void Flood(int i,int group)
            {
                groups[i]=group;
                for(int j=0;j<groups.Length;j++)if(j!=index&&groups[j]<0&&previous[j]==previous[i])
                {
                    var d=pieces[j].anchoredPosition-pieces[i].anchoredPosition;
                    if(Mathf.Abs(Mathf.Abs(d.x)-Pitch.x)<.01f&&Mathf.Abs(d.y)<.01f || Mathf.Abs(Mathf.Abs(d.y)-Pitch.y)<.01f&&Mathf.Abs(d.x)<.01f)Flood(j,group);
                }
            }
            for(int i=0;i<groups.Length;i++)if(groups[i]<0)Flood(i,i);
            motor.Rebase(entrance);PlaceMarker();
            RefreshSelectionHighlight();
        }
        bool WithinArea(int group, Vector2 delta)
        {
            for (int i = 0; i < pieces.Length; i++) if (groups[i] == group)
            {
                Vector2 p = pieces[i].anchoredPosition + delta;
                if (p.x < -.01f || p.y > .01f || p.x + pieces[i].rect.width > movementArea.rect.width + .01f || -p.y + pieces[i].rect.height > movementArea.rect.height + .01f) return false;
            }
            return true;
        }
        public void DragGroup(int index, Vector2 delta)
        {
            if (window.IsSolved) return;
            if (UsesSocketBoard) { DragSocketPiece(index, delta); return; }
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity, minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
            for (int i = 0; i < pieces.Length; i++) if (SameGroup(index, i))
            {
                var p = pieces[i].anchoredPosition; minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x + pieces[i].rect.width);
                maxY = Mathf.Max(maxY, p.y); minY = Mathf.Min(minY, p.y - pieces[i].rect.height);
            }
            delta.x = Mathf.Clamp(delta.x, -minX, movementArea.rect.width - maxX);
            delta.y = Mathf.Clamp(delta.y, -movementArea.rect.height - minY, -maxY);
            for (int i = 0; i < pieces.Length; i++) if (SameGroup(index, i)) pieces[i].anchoredPosition += delta;
            PlaceMarker();
        }
        public bool TrySnap(int index)
        {
            if (window.IsSolved) return false;
            if (UsesSocketBoard) return SnapToSocket(index);
            bool joined = false, changed;
            do
            {
                changed = false; float best = snapDistance; int a = -1, b = -1; Vector2 correction = Vector2.zero;
                for (int i = 0; i < pieces.Length; i++) if (SameGroup(index, i))
                    for (int j = 0; j < pieces.Length; j++) if (!SameGroup(i, j))
                      foreach(var direction in Directions)
                    {
                        var delta = pieces[j].anchoredPosition + new Vector2(direction.x*Pitch.x,-direction.y*Pitch.y) - pieces[i].anchoredPosition;
                        if (delta.magnitude > best || !WithinArea(groups[index], delta) || !FitsSquare(groups[index],groups[j],delta)) continue;
                        best = delta.magnitude; a = i; b = j; correction = delta;
                    }
                if (a >= 0)
                {
                    int old = groups[index], target = groups[b];
                    for (int i = 0; i < pieces.Length; i++) if (groups[i] == old) { pieces[i].anchoredPosition += correction; groups[i] = target; }
                    changed = joined = true; pulse = snapPulseDuration;
                }
            } while (changed);
            if(joined)motor.Rebase(entrance);
            PlaceMarker(); RefreshSelectionHighlight(); return joined;
        }
        int PieceAt(Vector2Int cell) => Array.IndexOf(slots, new Vector2Int(cell.x / pieceDimensions.x, cell.y / pieceDimensions.y));
        public bool CanOccupy(Vector2Int cell) => UsesSocketBoard ? SocketCanOccupy(cell) : cell.x >= 0 && cell.y >= 0 && PieceAt(cell) >= 0 && Array.IndexOf(floorCells, cell) >= 0;
        public bool TryStep(Vector2Int cell, Vector2Int direction, out Vector2Int next)
        {
            next = cell;
            if (UsesSocketBoard) { next = cell + direction; return cell != exit && Mathf.Abs(direction.x)+Mathf.Abs(direction.y)==1 && CanOccupy(cell) && CanOccupy(next); }
            if(!IsAssembled || cell==exit || Mathf.Abs(direction.x)+Mathf.Abs(direction.y)!=1 || !CanOccupy(cell))return false;
            int i=PieceAt(cell);var local=cell-slots[i]*pieceDimensions;var target=local+direction;
            if(target.x>=0&&target.y>=0&&target.x<pieceDimensions.x&&target.y<pieceDimensions.y)next=slots[i]*pieceDimensions+target;
            else
            {
                var expected=pieces[i].anchoredPosition+new Vector2(direction.x*Pitch.x,-direction.y*Pitch.y);
                int j=Array.FindIndex(pieces,p=>Vector2.Distance(p.anchoredPosition,expected)<.01f);
                if(j<0)return false;
                target=new Vector2Int((target.x+pieceDimensions.x)%pieceDimensions.x,(target.y+pieceDimensions.y)%pieceDimensions.y);
                next=slots[j]*pieceDimensions+target;
            }
            return CanOccupy(next);
        }
        public bool HasPort(Vector2Int cell, Vector2Int direction) => TryStep(cell, direction, out _);
        public Vector2Int DirectionBetween(Vector2Int from,Vector2Int to)
        {
            if (UsesSocketBoard) return to-from;
            int a=PieceAt(from),b=PieceAt(to);
            if(a==b)return to-from;
            var delta=pieces[b].anchoredPosition-pieces[a].anchoredPosition;
            return new Vector2Int(Mathf.RoundToInt(delta.x/Pitch.x),-Mathf.RoundToInt(delta.y/Pitch.y));
        }
        public float EdgeLimit(Vector2Int cell, Vector2Int direction) => 0;
        public Vector3 CellWorldPosition(Vector2Int cell)
        {
            if (UsesSocketBoard) return SocketCellWorldPosition(cell);
            int i = PieceAt(cell); var face = faces[i]; Vector2 size = face.rect.size / (Vector2)pieceDimensions;
            return face.TransformPoint(new Vector3(face.rect.xMin + (cell.x % pieceDimensions.x + .5f) * size.x, face.rect.yMax - (cell.y % pieceDimensions.y + .5f) * size.y));
        }
        public Vector3 OffsetWorldVector(Vector2Int cell, Vector2 offset)
        {
            if (UsesSocketBoard) return socketBoard.TransformVector(new Vector3(offset.x * socketBoard.rect.width / (3 * pieceDimensions.x), -offset.y * socketBoard.rect.height / (3 * pieceDimensions.y)));
            var face = faces[PieceAt(cell)]; Vector2 size = face.rect.size / (Vector2)pieceDimensions;
            return face.TransformVector(new Vector3(offset.x * size.x, -offset.y * size.y));
        }
        public void PressDirection(Vector2Int direction) { if (!window.IsSolved) motor.PressStep(direction); }
        public void Move(Vector2Int direction)
        {
            if (window.IsSolved) return;
            motor.AdvanceSteps(direction, Time.unscaledDeltaTime); PlaceMarker(); if (IsAssembled && motor.Reached(exit)) window.Complete();
        }
        public void Advance(Vector2Int direction, float dt)
        {
            if (window.IsSolved || !IsAssembled) return;
            motor.Move(direction, Mathf.Max(0, dt)); PlaceMarker();
            if (IsAssembled && motor.Reached(exit)) window.Complete();
        }
        public void Interact() { if (!window.IsSolved && IsAssembled) { motor.Rebase(entrance); PlaceMarker(); } }
        void LateUpdate()
        {
            if (UsesSocketBoard) SyncSocketPieces();
            pulse = Mathf.Max(0, pulse - Time.unscaledDeltaTime);
            for(int i=0;i<joinFeedback.Length;i++) joinFeedback[i].enabled = pulse > 0 && IsPieceSelected(i);
            SyncPresentation();
            PlaceMarker();
        }
        public void RefreshMarkerPosition() => PlaceMarker();
        void PlaceMarker()
        {
            if(playerMarker==null||faces.Length!=4)return;
            if (UsesSocketBoard) { PlaceSocketMarker(); return; }
            var parent=pieces[PieceAt(motor.Current)];
            if(playerMarker.transform.parent!=parent)playerMarker.transform.SetParent(parent,true);
            playerMarker.rectTransform.position=motor.WorldPosition;
        }
    }
}
