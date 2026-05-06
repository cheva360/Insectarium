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

        [Tooltip("Number of characters to delete before typing this word. -1 deletes all committed text.")]
        public int deleteCharsBefore = 0;

        [Tooltip("How many characters to delete per step. Increase to delete faster without going fully instant.")]
        public int deletionCharsPerStep = 0;

        [Tooltip("Delay in seconds between each deletion step. Set to -1 to delete all instantly.")]
        public float deletionSpeed = 0.05f;
    }

    [Tooltip("Audio clip to play when this decoder entry starts.")]
    public AudioClip startAudio;

    public WordEntry[] words;
}