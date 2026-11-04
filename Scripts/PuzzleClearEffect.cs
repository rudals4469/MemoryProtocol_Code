using System;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryProtocol
{
    // 참조와 외형은 편집 모드에서 저장하고, 런타임에는 기존 창만 움직인다.
    public sealed class PuzzleClearEffect : MonoBehaviour
    {
        public StoryProgression story;
        public MemoryFlightVisual memoryVisual;
        Vector3 flightOrigin;
        public Canvas[] flightCanvases = Array.Empty<Canvas>();
        public Graphic[] flightArtwork = Array.Empty<Graphic>();
        public bool blackSilhouette = true;
        Color[] artworkColors;
        RectTransform flightTarget;
        float flightDuration;
        bool[] canvasOverrides;
        int[] canvasOrders;
        public RectTransform[] targets = Array.Empty<RectTransform>();
        public CanvasGroup[] groups = Array.Empty<CanvasGroup>();
        public Image[] borders = Array.Empty<Image>();
        [Min(.05f)] public float duration = .4f;
        [Range(1, 1.05f)] public float reboundScale = 1.02f;
        [Range(.5f, 1)] public float finalScale = .85f;
        public Color flashColor = new Color(.88f, .94f, .87f, 1);
        Vector3[] positions, scales, centers;
        float[] alphas;
        bool[] interactions, raycasts;
        Color[] colors;
        float elapsed;
        PuzzleWindow owner;
        public bool IsPlaying { get; private set; }

        public void Play(PuzzleWindow window)
        {
            if (story != null) story.Enqueue(window, this);
            else PlayLocal(window);
        }

        public void PlayFlight(PuzzleWindow window, RectTransform destination, float seconds)
        {
            PlayLocal(window); flightTarget = destination; flightDuration = seconds;
            if (blackSilhouette)
            {
                artworkColors = new Color[flightArtwork.Length];
                for (int i = 0; i < flightArtwork.Length; i++) artworkColors[i] = flightArtwork[i].color;
            }
            if (memoryVisual != null) memoryVisual.Begin(this, flightOrigin);
            canvasOverrides = new bool[flightCanvases.Length];
            canvasOrders = new int[flightCanvases.Length];
            for (int i = 0; i < flightCanvases.Length; i++)
            {
                canvasOverrides[i] = flightCanvases[i].overrideSorting;
                canvasOrders[i] = flightCanvases[i].sortingOrder;
                flightCanvases[i].overrideSorting = true;
                flightCanvases[i].sortingOrder = 50;
                MaskUtilities.Notify2DMaskStateChanged(flightCanvases[i]);
                MaskUtilities.NotifyStencilStateChanged(flightCanvases[i]);
            }
        }

        public void PlayLocal(PuzzleWindow window)
        {
            Restore();
            owner = window;
            positions = new Vector3[targets.Length];
            scales = new Vector3[targets.Length];
            centers = new Vector3[targets.Length];
            var bounds = new Bounds();
            bool first = true;
            var corners = new Vector3[4];
            foreach (var target in targets)
            {
                target.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    if (first) { bounds = new Bounds(corner, Vector3.zero); first = false; }
                    else bounds.Encapsulate(corner);
                }
            }
            flightOrigin = bounds.center;
            for (int i = 0; i < targets.Length; i++)
            {
                positions[i] = targets[i].localPosition;
                scales[i] = targets[i].localScale;
                centers[i] = targets[i].parent.InverseTransformPoint(bounds.center);
            }
            alphas = new float[groups.Length]; interactions = new bool[groups.Length]; raycasts = new bool[groups.Length];
            for (int i = 0; i < groups.Length; i++)
            {
                alphas[i] = groups[i].alpha; interactions[i] = groups[i].interactable;
                raycasts[i] = groups[i].blocksRaycasts;
                groups[i].interactable = false; groups[i].blocksRaycasts = false;
            }
            colors = new Color[borders.Length];
            for (int i = 0; i < borders.Length; i++) colors[i] = borders[i].color;
            elapsed = 0; IsPlaying = true;
            Advance(0);
        }

        void Update() { if (IsPlaying) Advance(Time.unscaledDeltaTime); }

        // 편집기 검증도 실제 재생과 같은 시간 진행을 사용한다.
        public void Advance(float deltaTime)
        {
            if (!IsPlaying) return;
            elapsed += Mathf.Max(0, deltaTime);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, flightTarget != null ? flightDuration : duration));
            float scale = t < .2f ? Mathf.Lerp(1, reboundScale, Mathf.SmoothStep(0, 1, t / .2f))
                : t < .4f ? Mathf.Lerp(reboundScale, 1, Mathf.SmoothStep(0, 1, (t - .2f) / .2f))
                : Mathf.Lerp(1, finalScale, Mathf.SmoothStep(0, 1, (t - .4f) / .6f));
            float fade = Mathf.SmoothStep(0, 1, (t - .4f) / .6f);
            if (flightTarget != null)
            {
                scale = t < .15f ? Mathf.Lerp(1, reboundScale, t / .15f)
                    : Mathf.Lerp(reboundScale, .045f, Mathf.SmoothStep(0, 1, (t - .15f) / .85f));
                fade = Mathf.SmoothStep(0, 1, (t - .88f) / .12f);
            }
            if (flightTarget != null && memoryVisual != null)
            {
                scale = Mathf.Lerp(1,.025f,Mathf.SmoothStep(0,1,t/.3f));
                fade = Mathf.SmoothStep(0,1,(t-.12f)/.18f);
                memoryVisual.Advance(this,t,flightTarget.TransformPoint(flightTarget.rect.center));
            }
            float flash = t < .15f ? Mathf.SmoothStep(0, 1, t / .15f) : 1 - Mathf.SmoothStep(0, 1, (t - .15f) / .5f);
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i].localScale = scales[i] * scale;
                Vector3 center = centers[i];
                if (flightTarget != null && memoryVisual == null)
                {
                    float travel = Mathf.SmoothStep(0, 1, (t - .15f) / .85f);
                    Vector3 destination = targets[i].parent.InverseTransformPoint(flightTarget.TransformPoint(flightTarget.rect.center));
                    center = Vector3.Lerp(centers[i], destination, travel);
                    center.y += Mathf.Sin(travel * Mathf.PI) * 32;
                }
                targets[i].localPosition = center + (positions[i] - centers[i]) * scale;
            }
            for (int i = 0; i < groups.Length; i++) groups[i].alpha = alphas[i] * (1 - fade);
            for (int i = 0; i < borders.Length; i++) borders[i].color = Color.Lerp(colors[i], flashColor, flash);
            if (flightTarget != null && artworkColors != null)
            {
                // Become solid ink during the initial rebound, before any travel begins.
                float ink = Mathf.SmoothStep(0, 1, t / .15f);
                for (int i = 0; i < flightArtwork.Length; i++)
                {
                    var original = artworkColors[i];
                    flightArtwork[i].color = Color.Lerp(original, new Color(0, 0, 0, original.a), ink);
                }
            }
            if (t >= 1)
            {
                var finished = owner; Restore(); finished.FinishRemoval();
            }
        }

        public void Restore()
        {
            IsPlaying = false;
            if (memoryVisual != null) memoryVisual.Finish(this);
            flightTarget = null;
            if (artworkColors != null)
                for (int i = 0; i < flightArtwork.Length; i++)
                    if (flightArtwork[i] != null) flightArtwork[i].color = artworkColors[i];
            artworkColors = null;
            if (canvasOverrides != null)
                for (int i = 0; i < flightCanvases.Length; i++)
                    if (flightCanvases[i] != null)
                    {
                        flightCanvases[i].sortingOrder = canvasOrders[i];
                        flightCanvases[i].overrideSorting = canvasOverrides[i];
                        MaskUtilities.Notify2DMaskStateChanged(flightCanvases[i]);
                        MaskUtilities.NotifyStencilStateChanged(flightCanvases[i]);
                    }
            canvasOverrides = null;
            if (positions == null) return;
            for (int i = 0; i < targets.Length; i++)
                if (targets[i] != null) { targets[i].localPosition = positions[i]; targets[i].localScale = scales[i]; }
            for (int i = 0; i < groups.Length; i++)
                if (groups[i] != null) { groups[i].alpha = alphas[i]; groups[i].interactable = interactions[i]; groups[i].blocksRaycasts = raycasts[i]; }
            for (int i = 0; i < borders.Length; i++) if (borders[i] != null) borders[i].color = colors[i];
            positions = null;
        }
        void OnDisable() => Restore();
    }
}
