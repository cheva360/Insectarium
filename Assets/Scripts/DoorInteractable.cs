using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


public class DoorInteractable : MonoBehaviour
{
    private bool _coroutineStarted = false;

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
        Volume postProcessingVolume = UIController.Instance.PostProcessingVolume;
        UIController.Instance.PostProcessingVolume.profile.TryGet(out DepthOfField dof);
        UIController.Instance.PostProcessingVolume.profile.TryGet(out FilmGrain filmGrain);
        UIController.Instance.PostProcessingVolume.profile.TryGet(out MotionBlur motionBlur);
        float duration = 4f; // Duration of the blur effect
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            //depth of field
            dof.focalLength.value = Mathf.Lerp(0f, 200f, elapsedTime / duration);

            //move speed
            playerController.Instance.moveSpeed = Mathf.Lerp(3.5f, 1f, elapsedTime / duration);

            //film grain
            filmGrain.intensity.value = Mathf.Lerp(0f, 0.5f, elapsedTime / duration);

            //motion blur
            motionBlur.intensity.value = Mathf.Lerp(0f, 1f, elapsedTime / duration);

            //camera shake
            UIController.Instance.ShakeMagnitude = Mathf.Lerp(0f, 0.005f, elapsedTime / duration);

            yield return null;
        }

        // Wait for 2 seconds
        yield return new WaitForSeconds(.3f);

        // Set move speed to 0
        //playerController.Instance.moveSpeed = 0f;

        // Lerp player Z rotation to 90 degrees
        float rotateDuration = 2f;
        float rotateElapsed = 0f;
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
            filmGrain.intensity.value = Mathf.Lerp(0.5f, 0.7f, rotateElapsed / rotateDuration);

            yield return null;
        }
        float finalFadeDuration = .4f;
        float finalFadeElapsed = 0f;


        while (finalFadeElapsed < finalFadeDuration)
        {
            finalFadeElapsed += Time.deltaTime;
            playerController.Instance.SetState(playerController.playerState.Cutscene);

            float t = finalFadeElapsed / finalFadeDuration;
            float easedT = t * t * t; // Cubic ease-in: slow start, then drops hard

            // Rotate player x rotation from -30 to -80
            Vector3 currentEuler = playerController.Instance.transform.eulerAngles;
            currentEuler.x = Mathf.Lerp(-30f, -80f, easedT);
            playerController.Instance.transform.eulerAngles = currentEuler;

            // Fade to black
            UIController.Instance.Fade.color = new Color(0, 0, 0, Mathf.Lerp(0.8f, 0.99f, easedT));

            // Worsen film grain
            filmGrain.intensity.value = Mathf.Lerp(0.7f, 1f, easedT);

            yield return null;
        }
        UIController.Instance.Fade.color = new Color(0, 0, 0, 1);
        UIController.Instance.ShakeMagnitude = 0f;
        //set rotation to 0
        playerController.Instance.transform.eulerAngles = new Vector3(0f, -90f, 0f);
        //reset move speed
        playerController.Instance.moveSpeed = 3.5f;
        //reset effects
        dof.focalLength.value = 0f;
        motionBlur.intensity.value = 0f;
        //reset camera shake
        UIController.Instance.ShakeMagnitude = 0f;

        //wait for 5 seconds
        yield return new WaitForSeconds(5f);
        StartCoroutine(FadeInCoroutine());

    }

    IEnumerator FadeInCoroutine()
    {
        GameController.Instance.player.transform.position = GameController.Instance.SpawnLocation.position;
        playerController.Instance.SetState(playerController.playerState.Normal);

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
        _coroutineStarted = false;
    }
}
