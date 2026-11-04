using UnityEngine;

namespace MemoryProtocol
{
    // 일반 미로·동시 미로·박스 밀기의 입력 타이밍을 공유한다. 기믹은 각 퍼즐이 처리한다.
    public sealed class MazeStepInput
    {
        private Vector2Int held, pending;
        private float repeatRemaining;
        private bool freshPress;
        public float NextStepElapsed { get; private set; }

        public void Reset()
        {
            held = pending = Vector2Int.zero;
            repeatRemaining = 0;
            freshPress = false;
            NextStepElapsed = 0;
        }

        public bool Press(Vector2Int direction, bool moving, float delay)
        {
            held = direction;
            repeatRemaining = Mathf.Max(.1f, delay);
            pending = moving ? direction : Vector2Int.zero;
            freshPress = true;
            return !moving;
        }

        public void Queue(Vector2Int direction) => pending = direction;

        public Vector2Int Advance(Vector2Int direction, float elapsed, bool moving, float delay, float interval, float availableAfterMove = 0)
        {
            NextStepElapsed = 0;
            // 키를 놓으면 진행 중인 한 칸만 끝낸다. 예약된 다음 칸과 반복 입력은 남기지 않는다.
            if (direction == Vector2Int.zero) { Reset(); return Vector2Int.zero; }
            if (direction != held)
            {
                held = direction;
                freshPress = false;
                repeatRemaining = Mathf.Max(.1f, delay);
                pending = moving ? direction : Vector2Int.zero;
                return moving ? Vector2Int.zero : direction;
            }
            // 로딩/정지 직후 긴 프레임은 키를 누르기 전의 시간을 포함한다.
            // 첫 업데이트 하나로 첫 이동과 유지 반복을 함께 시작하지 않는다.
            if (freshPress && elapsed > Mathf.Max(delay, interval) * 2) { elapsed = 0; availableAfterMove = 0; }
            freshPress = false;
            repeatRemaining -= Mathf.Max(0, elapsed);
            if (moving) return Vector2Int.zero;
            if (pending != Vector2Int.zero)
            {
                var next = pending; pending = Vector2Int.zero;
                NextStepElapsed = elapsed < interval ? Mathf.Min(Mathf.Max(0, availableAfterMove), Mathf.Max(0, elapsed)) : 0;
                repeatRemaining = Mathf.Max(.1f, delay) - NextStepElapsed;
                return next;
            }
            if (repeatRemaining > .00001f) return Vector2Int.zero;
            // 긴 프레임에도 다음 한 칸만 시작하여 여러 칸이 보이지 않게 건너뛰지 않는다.
            // Preserve the sub-frame remainder instead of stopping at every cell boundary.
            // Long frames still start at most one next step, without skipping its animation.
            NextStepElapsed = elapsed < interval ? Mathf.Min(Mathf.Max(0, availableAfterMove), Mathf.Max(0, -repeatRemaining)) : 0;
            repeatRemaining = Mathf.Max(.05f, interval) - NextStepElapsed;
            return direction;
        }
    }
}
