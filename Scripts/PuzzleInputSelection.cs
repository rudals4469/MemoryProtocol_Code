using UnityEngine;
using UnityEngine.EventSystems;
namespace MemoryProtocol
{
    public sealed class PuzzleInputSelection : MonoBehaviour,IPointerDownHandler
    {
        public SharedPuzzleControls controls;
        public MonoBehaviour targetPuzzle;
        public void OnPointerDown(PointerEventData data)
        {if(data.button==PointerEventData.InputButton.Left && targetPuzzle!=null &&
            !(targetPuzzle.GetComponentInParent<PuzzleWindow>() is PuzzleWindow window && window.IsSolved))controls.Select(targetPuzzle);}
    }
}
