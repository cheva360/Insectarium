using System;
using UnityEngine;

[CreateAssetMenu(fileName = "DecoderWordData", menuName = "Insectarium/Decoder Word Data")]
public class DecoderWordData : ScriptableObject
{
    [Serializable]
    public class WordEntry
    {
        [Tooltip("The word or phrase to display.")]
        public string word;

        [Tooltip("Total time in seconds to type out the entire word.")]
        public float typewriterDuration = 1f;

        [Tooltip("Delay in seconds after this word finishes before the next word begins.")]
        public float delayAfterWord = 0.5f;
    }

    public WordEntry[] words;
}