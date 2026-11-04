using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MemoryProtocol
{
    [Serializable]
    public sealed class StoryReward
    {
        public int page;
        public bool opensTab;
        public RectTransform destination;
        public CanvasGroup[] scribbles = Array.Empty<CanvasGroup>();
    }

    // All text, covers, tabs and references are authored in the scene.
    public sealed class StoryProgression : MonoBehaviour
    {
        public StoryPageTabs tabs;
        public ScrollRect[] scrolls;
        public StoryReward[] rewards;
        [Min(.1f)] public float flightDuration = .95f;
        [Min(.05f)] public float revealDuration = .35f;
        [HideInInspector] public int coverLayoutVersion;
        public int CompletedCount { get; private set; }
        public bool IsAnimating => activeEffect != null || pending.Count > 0;
        readonly Queue<(PuzzleWindow window, PuzzleClearEffect effect)> pending = new();
        readonly HashSet<PuzzleWindow> credited = new();
        PuzzleWindow activeWindow;
        PuzzleClearEffect activeEffect;
        StoryReward activeReward;
        bool[] unlocked;
        bool arrived;
        float revealTime;
        Vector3 tabScale;

        void Awake() => ResetStory();

        public void ResetStory()
        {
            if (activeEffect != null) activeEffect.Restore();
            if (activeReward != null && activeReward.opensTab)
                tabs.buttons[activeReward.page].transform.localScale = tabScale;
            foreach (var item in pending) if (item.effect != null) item.effect.Restore();
            pending.Clear(); credited.Clear();
            activeWindow = null; activeEffect = null; activeReward = null;
            CompletedCount = 0; arrived = false;
            unlocked = new bool[tabs.pages.Length]; unlocked[0] = true;
            foreach (var reward in rewards)
                foreach (var cover in reward.scribbles) { cover.alpha = 1; cover.gameObject.SetActive(true); }
            foreach (var scroll in scrolls) { scroll.StopMovement(); scroll.verticalNormalizedPosition = 1; }
            foreach (var button in tabs.buttons) button.interactable = true;
            tabs.ResetNotifications(); tabs.SetUnlocked(unlocked); tabs.SelectPage(0);
        }

        public void Enqueue(PuzzleWindow window, PuzzleClearEffect effect)
        {
            // F7 resets can replay a puzzle, but must not grant the same stage reward twice.
            if (!credited.Add(window)) { effect.PlayLocal(window); return; }
            pending.Enqueue((window, effect));
        }

        void Update() => Advance(Time.unscaledDeltaTime);

        public void Advance(float deltaTime)
        {
            if (activeEffect == null)
            {
                while (pending.Count > 0)
                {
                    var item = pending.Dequeue();
                    if (item.window == null || !item.window.IsSolved)
                    { credited.Remove(item.window); continue; }
                    if (CompletedCount >= rewards.Length) { item.effect.PlayLocal(item.window); continue; }
                    activeWindow = item.window; activeEffect = item.effect;
                    activeReward = rewards[CompletedCount]; arrived = false; revealTime = 0;
                    Canvas.ForceUpdateCanvases();
                    tabScale = tabs.buttons[activeReward.page].transform.localScale;
                    activeEffect.PlayFlight(activeWindow, (RectTransform)tabs.buttons[activeReward.page].transform, flightDuration);
                    if (!activeWindow.gameObject.activeInHierarchy) activeEffect.Advance(flightDuration);
                    break;
                }
                return;
            }
            if (activeEffect.IsPlaying) return;
            if (!activeWindow.IsSolved && !arrived)
            {
                foreach (var button in tabs.buttons) button.interactable = true;
                credited.Remove(activeWindow); activeWindow = null; activeEffect = null; activeReward = null;
                return;
            }
            if (!arrived)
            {
                arrived = true; CompletedCount++;
                if (activeReward.opensTab)
                {
                    unlocked[activeReward.page] = true; tabs.SetUnlocked(unlocked);
                    tabs.buttons[activeReward.page].transform.localScale = tabScale * .15f;
                }
                tabs.MarkUnlocked(activeReward.page);
            }
            revealTime += Mathf.Max(0, deltaTime);
            float t = Mathf.Clamp01(revealTime / revealDuration);
            foreach (var cover in activeReward.scribbles) cover.alpha = 1 - Mathf.SmoothStep(0, 1, t);
            if (activeReward.opensTab)
            {
                float pop = t < .7f ? Mathf.Lerp(.15f, 1.16f, Mathf.SmoothStep(0, 1, t / .7f))
                    : Mathf.Lerp(1.16f, 1, Mathf.SmoothStep(0, 1, (t - .7f) / .3f));
                tabs.buttons[activeReward.page].transform.localScale = tabScale * pop;
            }
            if (t < 1) return;
            foreach (var cover in activeReward.scribbles) cover.gameObject.SetActive(false);
            foreach (var button in tabs.buttons) button.interactable = true;
            activeWindow = null; activeEffect = null; activeReward = null;
        }
    }
}
