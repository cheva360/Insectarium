using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public class LoopTrigger : MonoBehaviour
{
    [SerializeField]LoopTriggerTest loopTriggerTest;

    private bool _coroutineStarted = false;

    private void OnEnable()
    {
        _coroutineStarted = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_coroutineStarted) return;

        if (other.CompareTag("Player"))
        {
            _coroutineStarted = true;
            StartCoroutine(BlurEffectCoroutine());
        }
    }

    IEnumerator BlurEffectCoroutine()
    {
        //play one shot passing out sound
        GameController.Instance.GameControllerAudioSource.PlayOneShot(GameController.Instance.passingOutSound);
        GameController.Instance.MusicFadeOut();   // ← fade out music with passing-out
        Volume postProcessingVolume = UIController.Instance.PostProcessingVolume;
        UIController.Instance.PostProcessingVolume.profile.TryGet(out DepthOfField dof);
        UIController.Instance.PostProcessingVolume.profile.TryGet(out FilmGrain filmGrain);
        UIController.Instance.PostProcessingVolume.profile.TryGet(out MotionBlur motionBlur);
        float duration = 4f;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            dof.focalLength.value               = Mathf.Lerp(0f, 200f, elapsedTime / duration);
            playerController.Instance.moveSpeed = Mathf.Lerp(3.5f, 1f, elapsedTime / duration);
            filmGrain.intensity.value           = Mathf.Lerp(0f, 0.5f, elapsedTime / duration);
            motionBlur.intensity.value          = Mathf.Lerp(0f, 1f, elapsedTime / duration);
            UIController.Instance.ShakeMagnitude = Mathf.Lerp(0f, 0.005f, elapsedTime / duration);
            yield return null;
        }

        // Wait for 2 seconds
        yield return new WaitForSeconds(.3f);

        // Set move speed to 0
        //playerController.Instance.moveSpeed = 0f;

        // Lerp player Z rotation to 90 degrees
        float rotateDuration = 2f;
        float rotateElapsed  = 0f;
        Transform camTransform = playerController.Instance.playerCamera.transform;
        float startX = camTransform.localPosition.x;


        while (rotateElapsed < rotateDuration)
        {
            float r = rotateElapsed / rotateDuration;
            float easedR = r * r;
            rotateElapsed += Time.deltaTime;

            //rotate player x rotation to -50
            Vector3 currentEuler = playerController.Instance.transform.eulerAngles;
            currentEuler.x = Mathf.Lerp(startX, -30f, easedR);
            playerController.Instance.transform.eulerAngles = currentEuler;
            Vector3 camPos = camTransform.localPosition;

            //fade to black
            UIController.Instance.Fade.color = new Color(0, 0, 0, Mathf.Lerp(0f, 0.8f, rotateElapsed / rotateDuration));

            //worsen film grain
            filmGrain.intensity.value        = Mathf.Lerp(0.5f, 0.7f, rotateElapsed / rotateDuration);

            yield return null;
        }
        float finalFadeDuration = .4f;
        float finalFadeElapsed  = 0f;


        while (finalFadeElapsed < finalFadeDuration)
        {
            finalFadeElapsed += Time.deltaTime;
            playerController.Instance.SetState(playerController.playerState.Cutscene);

            float t      = finalFadeElapsed / finalFadeDuration;
            float easedT = t * t * t; // Cubic ease-in: slow start, then drops hard

            // Rotate player x rotation from -30 to -80
            Vector3 currentEuler = playerController.Instance.transform.eulerAngles;
            currentEuler.x = Mathf.Lerp(-30f, -80f, easedT);
            playerController.Instance.transform.eulerAngles = currentEuler;

            // Fade to black
            UIController.Instance.Fade.color = new Color(0, 0, 0, Mathf.Lerp(0.8f, 0.99f, easedT));

            // Worsen film grain
            filmGrain.intensity.value        = Mathf.Lerp(0.7f, 1f, easedT);

            yield return null;
        }
        UIController.Instance.Fade.color     = new Color(0, 0, 0, 1);
        UIController.Instance.ShakeMagnitude = 0f;
        playerController.Instance.transform.eulerAngles = new Vector3(0f, -90f, 0f);
        playerController.Instance.moveSpeed  = 3.5f;
        dof.focalLength.value                = 0f;
        motionBlur.intensity.value           = 0f;
        UIController.Instance.ShakeMagnitude = 0f;

        yield return StartCoroutine(LoadingScreenController.Instance.Play());

        // ── End of Demo ───────────────────────────────────────────────────────
        // Show only the Quit to Menu button/text and End of Demo label.
        // The player stays here until they click Quit to Menu.
        if (endOfDemoRoot    != null) endOfDemoRoot.SetActive(true);
        if (quitToMenuButton != null) quitToMenuButton.SetActive(true);
        if (quitToMenuText   != null) quitToMenuText.SetActive(true);
        if (endOfDemoText    != null) endOfDemoText.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    IEnumerator FadeInCoroutine()
    {
        GameController.Instance.player.transform.position = GameController.Instance.SpawnLocation.position;
        playerController.Instance.SetState(playerController.playerState.Normal);
        GameController.Instance.MusicFadeIn();    // ← fade music back in with screen

        UIController.Instance.PostProcessingVolume.profile.TryGet(out FilmGrain filmGrain);
        filmGrain.intensity.value = 0f;

        //fade in from black
        float duration = 4f;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            UIController.Instance.Fade.color = new Color(0, 0, 0, Mathf.Lerp(1f, 0f, elapsedTime / duration));

            yield return null;

        }

        // Advance loop count (caps at Three)
        // reset cassette ui
        if (GameController.Instance.CurrentLoop < GameController.LoopCount.Three)
            GameController.Instance.CurrentLoop = (GameController.LoopCount)(GameController.Instance.CurrentLoop + 1);

        UIController.Instance.ResetUI();

        //disable looping triggering (will delete in the future)
        loopTriggerTest.LoopTriggered();

        _coroutineStarted = false;
    }

    [Header("End of Demo UI")]
    [Tooltip("Root that holds both the Quit To Menu button/text and the End of Demo label.")]
    [SerializeField] private GameObject endOfDemoRoot;
    [Tooltip("The 'Quit to Menu' button GameObject.")]
    [SerializeField] private GameObject quitToMenuButton;
    [Tooltip("The 'Quit to Menu' label text GameObject.")]
    [SerializeField] private GameObject quitToMenuText;
    [Tooltip("The 'End of Demo' label text GameObject.")]
    [SerializeField] private GameObject endOfDemoText;
}
