using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
namespace MemoryProtocol
{
    public interface ISharedPuzzleInput { void Move(Vector2Int direction); void Interact(); }
    public interface IPressedPuzzleInput { void PressDirection(Vector2Int direction); }
    public interface IBidirectionalPuzzleInput { void Interact(bool clockwise); }
    public sealed class SharedPuzzleControls : MonoBehaviour
    {
        public MonoBehaviour selectedPuzzle;
        public Image[] keyImages;
        public RectTransform[] keyRects;
        public Color pressedColor = new Color(.75f,.70f,.60f,1);
        private Vector2Int lastDirection;
        private Color[] normalColors;
        private Vector3[] normalScales;
        private readonly Key[] keys = {Key.W,Key.A,Key.S,Key.D,Key.Space,Key.Q};
        private readonly Vector2Int[] directions = {new Vector2Int(0,-1),Vector2Int.left,new Vector2Int(0,1),Vector2Int.right};
        public bool UsesRotationControls => selectedPuzzle != null && selectedPuzzle.isActiveAndEnabled && selectedPuzzle is IBidirectionalPuzzleInput;
        private void Awake()
        {
            normalColors=new Color[keyImages.Length]; normalScales=new Vector3[keyRects.Length];
            for(int i=0;i<keyImages.Length;i++) if(keyImages[i]!=null) normalColors[i]=keyImages[i].color;
            for(int i=0;i<keyRects.Length;i++) if(keyRects[i]!=null) normalScales[i]=keyRects[i].localScale;
            RefreshKeyVisibility();
        }
        public void Select(MonoBehaviour puzzle)
        {
            var previous=selectedPuzzle;
            selectedPuzzle=puzzle;
            if(previous!=null) previous.GetComponent<PuzzleSelectionHighlight>()?.RefreshSelection();
            if(selectedPuzzle!=null) selectedPuzzle.GetComponent<PuzzleSelectionHighlight>()?.RefreshSelection();
            RefreshKeyVisibility();
        }
        public void RefreshKeyVisibility()
        {
            bool rotation=UsesRotationControls;
            for(int i=0;i<keyRects.Length;i++) if(keyRects[i]!=null)
                keyRects[i].gameObject.SetActive(i<keys.Length && (!rotation || i==1 || i==3 || i==5));
        }
        private void Update() => ProcessKeyboard(Application.isFocused ? Keyboard.current : null);
        // The editor's headless input checks can feed the same keyboard path without OS focus.
        public void ProcessKeyboard(Keyboard keyboard)
        {
            // 테스트용: 선택한 퍼즐을 기존 완료/제거 흐름으로 처리한다.
            if(keyboard!=null && keyboard[Key.E].wasPressedThisFrame && selectedPuzzle!=null && selectedPuzzle.isActiveAndEnabled)
            {
                var window=selectedPuzzle.GetComponentInParent<PuzzleWindow>();
                if(window!=null && !window.IsSolved)
                {
                    lastDirection=Vector2Int.zero;
                    window.Complete();
                    return;
                }
            }
            bool resetRequested=false;
            if(keyboard!=null && keyboard[Key.Q].wasPressedThisFrame && selectedPuzzle!=null && selectedPuzzle.isActiveAndEnabled)
            {
                var window=selectedPuzzle.GetComponentInParent<PuzzleWindow>();
                if(window!=null && !window.IsSolved)
                {
                    window.ResetProgress();
                    lastDirection=Vector2Int.zero;
                    resetRequested=true;
                }
            }
            bool solved=selectedPuzzle!=null && selectedPuzzle.GetComponentInParent<PuzzleWindow>() is PuzzleWindow selectedWindow && selectedWindow.IsSolved;
            bool rotation=UsesRotationControls;
            RefreshKeyVisibility();
            for(int i=0;i<keys.Length;i++)
            {
                bool held=keyboard!=null && keyboard[keys[i]].isPressed;
                float blend=1-Mathf.Exp(-22*Time.unscaledDeltaTime);
                if(i<keyImages.Length && keyImages[i]!=null) keyImages[i].color=Color.Lerp(keyImages[i].color,held?pressedColor:normalColors[i],blend);
                if(i<keyRects.Length && keyRects[i]!=null) keyRects[i].localScale=Vector3.Lerp(keyRects[i].localScale,normalScales[i]*(held?.88f:1),blend);
                if(!resetRequested && !solved && !rotation && i<4 && keyboard!=null && keyboard[keys[i]].wasPressedThisFrame)
                {
                    lastDirection=directions[i];
                    if(selectedPuzzle!=null && selectedPuzzle.isActiveAndEnabled && selectedPuzzle is IPressedPuzzleInput pressed) pressed.PressDirection(directions[i]);
                }
            }
            if(resetRequested || solved || selectedPuzzle==null || !selectedPuzzle.isActiveAndEnabled || !(selectedPuzzle is ISharedPuzzleInput input)) return;
            if(rotation)
            {
                bool left=keyboard!=null && keyboard[Key.A].wasPressedThisFrame;
                bool right=keyboard!=null && keyboard[Key.D].wasPressedThisFrame;
                if(left!=right) ((IBidirectionalPuzzleInput)selectedPuzzle).Interact(right);
                return;
            }
            if(keyboard!=null && keyboard[Key.Space].wasPressedThisFrame) input.Interact();
            Vector2Int direction=Vector2Int.zero;
            if(keyboard!=null)
                for(int i=0;i<4;i++) if(keyboard[keys[i]].isPressed) {direction=directions[i]; if(direction==lastDirection) break;}
            input.Move(direction);
        }
    }
}
