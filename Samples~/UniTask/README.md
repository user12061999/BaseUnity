# UniTask pooling example

Install UniTask with **Tools > BaseUnity > Integrations > Install UniTask 2.5.11**.
Import this sample (UPM) or copy this folder into your game's Assets (submodule).
Attach `UniTaskPoolExample` to a GameObject and assign a prefab. Call `Spawn` from gameplay or a UI button.
Objects return after the configured lifetime. Enable `ignoreTimeScale` for a timer that continues while paused.

The owner disposes the pool on destruction. Cancellation cleanup uses the rental ID so an old async operation
cannot return an object that has already been rented again. Do not destroy pooled objects outside their pool.
The sample assembly enables itself when the `com.cysharp.unitask` UPM package is installed.
