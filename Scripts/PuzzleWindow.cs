using UnityEngine;

namespace MemoryProtocol
{
    public sealed class PuzzleWindow : MonoBehaviour
    {
        [SerializeField] private RectTransform movementArea;
        [SerializeField] private PuzzleTile[] tiles;
        [SerializeField] private LayeredPuzzlePrototype board;
        [SerializeField] private PuzzleClearEffect clearEffect;
        private RectTransform window;
        private Vector2 initialPosition;
        private Vector2 previousAreaSize;
        public bool IsSolved { get; private set; }

        private void Awake()
        {
            window = (RectTransform)transform;
            initialPosition = window.anchoredPosition;
        }

        public void ResetPuzzle()
        {
            if (window == null) Awake();
            if (clearEffect != null) clearEffect.Restore();
            IsSolved = false;
            gameObject.SetActive(true);
            window.anchoredPosition = initialPosition;
            foreach (PuzzleTile tile in tiles) tile.ResetTile();
            ClampPosition();
        }

        // Q resets puzzle progress while preserving the player's window/fragment placement.
        public void ResetProgress()
        {
            if (IsSolved) return;
            if (TryGetComponent<AssemblyMazePuzzle>(out var socketPuzzle) && socketPuzzle.UsesSocketBoard)
            {
                socketPuzzle.ResetSocketProgress();
                return;
            }
            var rect = (RectTransform)transform;
            Vector2 position = rect.anchoredPosition;
            var assembly = GetComponent<AssemblyMazePuzzle>();
            Vector2[] piecePositions = null;
            int selectedPiece = 0;
            if (assembly != null)
            {
                piecePositions = new Vector2[assembly.pieces.Length];
                for (int i = 0; i < piecePositions.Length; i++) piecePositions[i] = assembly.pieces[i].anchoredPosition;
                selectedPiece = assembly.SelectedPiece;
            }
            ResetState();
            rect.anchoredPosition = position;
            if (assembly != null)
            {
                for (int i = 0; i < piecePositions.Length; i++) assembly.pieces[i].anchoredPosition = piecePositions[i];
                assembly.SelectPiece(selectedPiece);
                assembly.RefreshMarkerPosition();
            }
        }

        // F7 also restores the authored window placement.
        public void ResetState()
        {
            foreach (var behaviour in GetComponents<MonoBehaviour>()) behaviour.StopAllCoroutines();
            gameObject.SetActive(false);
            ResetPuzzle();
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                switch (behaviour)
                {
                    case FoldingMazePuzzle p: p.ResetMaze(); break;
                    case ExpansionMazePuzzle p: p.InitializeAuthoredView(); break;
                    case RotationMazePuzzle p: p.InitializeAuthoredView(); break;
                    case SlidingMazePuzzle p: p.InitializeAuthoredView(); break;
                    case GravityMazePuzzle p: p.InitializeAuthoredView(); break;
                    case LinkedMazePuzzle p: p.InitializeAuthoredView(); break;
                    case ArithmeticRegionPuzzle p: p.InitializeAuthoredView(); break;
                    case GrowingSnakeMazePuzzle p: p.InitializeAuthoredView(); break;
                    case SokobanMazePuzzle p: p.InitializeAuthoredView(); break;
                    case TogglePaintPuzzle p: p.InitializeAuthoredView(); break;
                    case ColorSwitchMazePuzzle p: p.InitializeAuthoredView(); break;
                    case MoveBudgetPuzzle p: p.InitializeAuthoredView(); break;
                    case RegionDoorMazePuzzle p: p.InitializeAuthoredView(); break;
                    case AssemblyMazePuzzle p: p.InitializeAuthoredView(); break;
                }
            }
            if (board != null) board.RefreshProgress();
        }

        private void LateUpdate()
        {
            if (IsSolved || movementArea == null) return;
            Vector2 size = movementArea.rect.size;
            if (previousAreaSize != Vector2.zero && previousAreaSize != size)
            {
                Vector2 position = window.anchoredPosition;
                position.x *= size.x / Mathf.Max(1, previousAreaSize.x);
                position.y *= size.y / Mathf.Max(1, previousAreaSize.y);
                window.anchoredPosition = position;
            }
            previousAreaSize = size;
            ClampPosition();
        }

        public void MoveTo(Vector2 position)
        {
            if (IsSolved) return;
            window.anchoredPosition = position;
            ClampPosition();
        }

        private void ClampPosition()
        {
            if (movementArea == null) return;
            // 창은 왼쪽 위 앵커와 피벗으로 저장한다.
            Vector2 position = window.anchoredPosition;
            position.x = Mathf.Clamp(position.x, 0, Mathf.Max(0, movementArea.rect.width - window.rect.width));
            position.y = Mathf.Clamp(position.y, -Mathf.Max(0, movementArea.rect.height - window.rect.height), 0);
            window.anchoredPosition = position;
        }

        public void CheckSolved()
        {
            if (tiles == null || tiles.Length == 0) return;
            foreach (PuzzleTile tile in tiles) if (!tile.IsLit) return;
            Complete();
        }

        // 실제 퍼즐의 정답 판정도 이 메서드에 연결한다.
        public void Complete()
        {
            if (IsSolved) return;
            IsSolved = true;
            if (Application.isPlaying && clearEffect != null && isActiveAndEnabled)
                clearEffect.Play(this);
            else FinishRemoval();
        }

        public void FinishRemoval()
        {
            gameObject.SetActive(false);
            if (board != null) board.RefreshProgress();
        }
    }
}
