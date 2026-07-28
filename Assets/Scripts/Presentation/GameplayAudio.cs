using System.Collections.Generic;
using UnityEngine;

namespace AshesOfRum
{
    public enum GameplayCue
    {
        Selection,
        Order,
        Construction,
        Production,
        Attack,
        Hit,
        Warning,
        Victory,
        Defeat
    }

    [RequireComponent(typeof(AudioSource))]
    public sealed class GameplayAudio : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private readonly Dictionary<GameplayCue, AudioClip> clips = new();
        private readonly Dictionary<GameplayCue, int> playCounts = new();
        private AudioSource source;

        public GameplayCue? LastCue { get; private set; }
        public int CueCount { get; private set; }
        public bool HasAllFunctionalCues => clips.Count == 9;
        public int CountFor(GameplayCue cue) => playCounts.TryGetValue(cue, out var count) ? count : 0;

        public void Initialize()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0.35f;
            clips[GameplayCue.Selection] = CreateTone("Selection Cue", 620f, 0.07f, 0.18f);
            clips[GameplayCue.Order] = CreateTone("Order Cue", 470f, 0.09f, 0.2f);
            clips[GameplayCue.Construction] = CreateTone("Construction Cue", 260f, 0.14f, 0.22f);
            clips[GameplayCue.Production] = CreateTone("Production Cue", 720f, 0.13f, 0.22f);
            clips[GameplayCue.Attack] = CreateTone("Attack Cue", 180f, 0.08f, 0.16f);
            clips[GameplayCue.Hit] = CreateTone("Hit Cue", 120f, 0.06f, 0.16f);
            clips[GameplayCue.Warning] = CreatePulse("Warning Cue", 310f, 0.28f, 0.28f);
            clips[GameplayCue.Victory] = CreateSweep("Victory Cue", 440f, 880f, 0.55f, 0.3f);
            clips[GameplayCue.Defeat] = CreateSweep("Defeat Cue", 330f, 130f, 0.55f, 0.3f);
        }

        public void Play(GameplayCue cue)
        {
            if (source == null || !clips.TryGetValue(cue, out var clip)) return;
            LastCue = cue;
            CueCount++;
            playCounts[cue] = CountFor(cue) + 1;
            source.PlayOneShot(clip);
        }

        private static AudioClip CreateTone(string name, float frequency, float seconds, float amplitude)
            => CreateClip(name, seconds, (time, duration) =>
                Mathf.Sin(2f * Mathf.PI * frequency * time) * Envelope(time, duration) * amplitude);

        private static AudioClip CreatePulse(string name, float frequency, float seconds, float amplitude)
            => CreateClip(name, seconds, (time, duration) =>
                Mathf.Sin(2f * Mathf.PI * frequency * time) * Envelope(time, duration) * amplitude *
                (Mathf.Repeat(time, 0.12f) < 0.075f ? 1f : 0f));

        private static AudioClip CreateSweep(string name, float startFrequency, float endFrequency, float seconds,
            float amplitude) => CreateClip(name, seconds, (time, duration) =>
        {
            var progress = time / duration;
            var frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            return Mathf.Sin(2f * Mathf.PI * frequency * time) * Envelope(time, duration) * amplitude;
        });

        private static AudioClip CreateClip(string name, float seconds, System.Func<float, float, float> sample)
        {
            var count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (var index = 0; index < count; index++)
            {
                var time = index / (float)SampleRate;
                data[index] = sample(time, seconds);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Envelope(float time, float duration)
        {
            const float attack = 0.015f;
            var fadeIn = Mathf.Clamp01(time / attack);
            var fadeOut = Mathf.Clamp01((duration - time) / Mathf.Min(0.08f, duration * 0.45f));
            return fadeIn * fadeOut;
        }
    }
}
