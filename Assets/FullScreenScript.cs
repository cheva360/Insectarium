using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FullScreen
{
    private const string ReopenKey = "FullScreen.ReopenAfterPlay";
    private static EditorWindow _fullscreenView;

    static FullScreen()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.ExitingEditMode:
            case PlayModeStateChange.ExitingPlayMode:
                // Close before the domain reload orphans the window
                if (HasWindow())
                {
                    SessionState.SetBool(ReopenKey, true);
                    Close();
                }
                break;

            case PlayModeStateChange.EnteredPlayMode:
            case PlayModeStateChange.EnteredEditMode:
                if (SessionState.GetBool(ReopenKey, false))
                {
                    SessionState.SetBool(ReopenKey, false);
                    // Delay so the editor layout is ready
                    EditorApplication.delayCall += Open;
                }
                break;
        }
    }

    private static bool HasWindow()
    {
        return _fullscreenView != null;
    }

    // Ctrl+Shift+W toggles fullscreen
    [MenuItem("Window/Game Fullscreen %#w")]
    public static void Toggle()
    {
        if (_fullscreenView != null)
        {
            Close();
            return;
        }
        Open();
    }

    private static void Open()
    {
        if (_fullscreenView != null) return;

        Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
        _fullscreenView = (EditorWindow)ScriptableObject.CreateInstance(gameViewType);

        PropertyInfo showToolbar = gameViewType.GetProperty(
            "showToolbar", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        showToolbar?.SetValue(_fullscreenView, false);

        Resolution res = Screen.currentResolution;
        _fullscreenView.ShowPopup();
        _fullscreenView.position = new Rect(0, 0, res.width, res.height);
        _fullscreenView.Focus();
    }

    private static void Close()
    {
        if (_fullscreenView != null)
        {
            _fullscreenView.Close();
            _fullscreenView = null;
        }
    }
}