using System;
using UnityEngine;

namespace IAmJanitor.Rendering.Highlighting
{
    [Serializable]
    public struct HighlightStyle : IEquatable<HighlightStyle>
    {
        public Color color;
        [Range(0.5f, 6f)] public float widthPixels;
        public bool pulse;
        [Min(0.01f)] public float pulseFrequency;
        [Range(0f, 0.8f)] public float pulseAmount;
        [Tooltip("Task guidance may remain visible through walls. Hover uses normal depth-tested outlines.")]
        public bool throughWalls;

        public static HighlightStyle Hover => new HighlightStyle
        {
            color = Color.white,
            widthPixels = 2f,
            pulseFrequency = 1f,
            pulseAmount = 0.25f
        };

        public bool Equals(HighlightStyle other) => color.Equals(other.color)
            && widthPixels.Equals(other.widthPixels) && pulse == other.pulse
            && pulseFrequency.Equals(other.pulseFrequency) && pulseAmount.Equals(other.pulseAmount)
            && throughWalls == other.throughWalls;
    }

    [Serializable]
    public sealed class TaskHighlightSettings
    {
        public bool enabled = true;
        public Color color = Color.white;
        [Range(0.5f, 6f)] public float widthPixels = 2f;
        [Min(0.5f)] public float maximumDistance = 14f;
        [Range(1, 16)] public int maximumTargets = 4;
        [Tooltip("While a carryable is held, guide to a matching deposit point instead of more objectives.")]
        public bool guideCarriedItems = true;
        public bool pulse;
        [Min(0.01f)] public float pulseFrequency = 1f;
        [Range(0f, 0.8f)] public float pulseAmount = 0.25f;
        [Tooltip("Use a hollow through-wall rim for task guidance only. Keep the nearby target budget at four.")]
        public bool throughWalls;

        public HighlightStyle Style => new HighlightStyle
        {
            color = color,
            widthPixels = widthPixels,
            pulse = pulse,
            pulseFrequency = pulseFrequency,
            pulseAmount = pulseAmount,
            throughWalls = throughWalls
        };
    }
}
