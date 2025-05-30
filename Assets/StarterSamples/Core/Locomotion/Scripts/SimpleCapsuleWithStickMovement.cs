/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.GlobalIllumination;

using System.Runtime.Serialization.Formatters.Binary;//必须要导入的包
using System.IO;
using UnityEngine.UI;//必须要导入的包

public class SimpleCapsuleWithStickMovement : MonoBehaviour
{
    public bool EnableLinearMovement = true;
    public bool EnableRotation = true;
    public bool HMDRotatesPlayer = true;
    public bool RotationEitherThumbstick = false;
    public float RotationAngle = 1.0f;
    public float Speed = 0.0f;
    public OVRCameraRig CameraRig;

    private bool ReadyToSnapTurn;
    private Rigidbody _rigidbody;

    public event Action CameraUpdated;
    public event Action PreCharacterMove;

    public bool haveKey = false;
    public int enemyCount = 0;

    public float maxHealth = 1000f;
    public float currentHealth = 1000f;

    public Canvas WinCanvas;
    public Canvas NoKeyCanvas;
    public Canvas GameOverCanvas;
    public Canvas MenuCanvas;
    public Canvas BeginCanvas;
    public Canvas HealthCanvas;

    public GameObject spotlight;

    //游戏总时间
    public float gameTime = 0;

    public bool win = false;

    //private Vector3 OriginalHealthScale;


    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (CameraRig == null) CameraRig = GetComponentInChildren<OVRCameraRig>();
    }

    void Start()
    {
        StartCoroutine(HideCanvasAfterDelay(4f, BeginCanvas));

        //OriginalHealthScale = HealthCanvas.GetComponentInChildren<Slider>().transform.localScale;
    }

    private void FixedUpdate()
    {
        if (CameraUpdated != null) CameraUpdated();
        if (PreCharacterMove != null) PreCharacterMove();

        if (HMDRotatesPlayer) RotatePlayerToHMD();
        if (EnableLinearMovement) StickMovement();
        if (EnableRotation) SnapTurn();

        if (spotlight != null)
        {
            spotlight.transform.position = transform.position;
            spotlight.transform.rotation = transform.rotation;
        }

        SwitchBGM();

        //游戏总时间
        if (!win)
        {
            gameTime += Time.fixedDeltaTime;
            WinCanvas.GetComponentInChildren<TextMeshProUGUI>().text = "Congratulations!\n    Time: " + gameTime.ToString("F2") + "s";
        }

        //血条显示
        HealthCanvas.GetComponentsInChildren<TextMeshProUGUI>()[0].text = "HP: " + currentHealth.ToString("F0") + "/" + maxHealth.ToString("F0");
        HealthCanvas.GetComponentInChildren<Slider>().value = currentHealth / maxHealth;

        //血条总长度随maxHealth变化
        //HealthCanvas.GetComponentInChildren<Slider>().transform.localScale = new Vector3(maxHealth / 1000f * OriginalHealthScale.x, OriginalHealthScale.y, OriginalHealthScale.z);
    }

    private float updateDelay = 0.1f; // Delay in seconds

    private float updateTimer = 0.0f;

    //每一秒执行一次的函数
    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateDelay)
        {
            updateTimer = 0.0f;
            //获取OVR按键
            if (OVRInput.Get(OVRInput.Button.Start))
            {
                //按两次退出游戏,第一次显示菜单,第二次退出游戏
                if (MenuCanvas.gameObject.activeSelf)
                {
                    SceneManager.LoadScene("StartScene");
                }
                else
                {
                    MenuCanvas.gameObject.SetActive(true);
                    //防止重复按键
                }
            }
        }
    }

    void SwitchBGM()
    {
        if (enemyCount == 0 && BGMManager.instance.isAttack)
        {
            BGMManager.instance.isAttack = false;
            BGMManager.instance.NormalBGM();
        }
        else if (enemyCount > 0 && !BGMManager.instance.isAttack)
        {
            BGMManager.instance.isAttack = true;
            BGMManager.instance.AttackBGM();
        }
    }


    void RotatePlayerToHMD()
    {
        Transform root = CameraRig.trackingSpace;
        Transform centerEye = CameraRig.centerEyeAnchor;

        Vector3 prevPos = root.position;
        Quaternion prevRot = root.rotation;

        Quaternion targetRotation = Quaternion.Euler(0.0f, centerEye.rotation.eulerAngles.y, 0.0f);
        //float rotationSpeed = 2.0f; // Adjust the rotation speed as needed

        // Slowly rotate towards the target rotation
        //transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        transform.rotation = targetRotation;

        root.position = prevPos;
        root.rotation = prevRot;
    }

    void StickMovement()
    {
        Quaternion ort = CameraRig.centerEyeAnchor.rotation;
        Vector3 ortEuler = ort.eulerAngles;
        ortEuler.z = ortEuler.x = 0f;
        ort = Quaternion.Euler(ortEuler);

        Vector3 moveDir = Vector3.zero;
        Vector2 primaryAxis = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        moveDir += ort * (primaryAxis.x * Vector3.right);
        moveDir += ort * (primaryAxis.y * Vector3.forward);
        //_rigidbody.MovePosition(_rigidbody.transform.position + moveDir * Speed * Time.fixedDeltaTime);
        _rigidbody.MovePosition(_rigidbody.position + moveDir * Speed * Time.fixedDeltaTime);
    }

    void SnapTurn()
    {
        if (OVRInput.Get(OVRInput.Button.SecondaryThumbstickLeft) ||
            (RotationEitherThumbstick && OVRInput.Get(OVRInput.Button.PrimaryThumbstickLeft)))
        {
            if (ReadyToSnapTurn)
            {
                ReadyToSnapTurn = false;
                transform.RotateAround(CameraRig.centerEyeAnchor.position, Vector3.up, -RotationAngle);
            }
        }
        else if (OVRInput.Get(OVRInput.Button.SecondaryThumbstickRight) ||
                 (RotationEitherThumbstick && OVRInput.Get(OVRInput.Button.PrimaryThumbstickRight)))
        {
            if (ReadyToSnapTurn)
            {
                ReadyToSnapTurn = false;
                transform.RotateAround(CameraRig.centerEyeAnchor.position, Vector3.up, RotationAngle);
            }
        }
        else
        {
            ReadyToSnapTurn = true;
        }
    }

    private IEnumerator SlowRotateAround(Vector3 center, Vector3 axis, float angle, float duration)
    {
        float elapsedTime = 0f;
        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = Quaternion.AngleAxis(angle, axis) * startRotation;

        while (elapsedTime < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "Keys")
        {
            EEFManager.instance.GetAudio();
            haveKey = true;
            Destroy(other.gameObject);
            HealthCanvas.GetComponentsInChildren<TextMeshProUGUI>()[1].text = null;
        }

        if (other.gameObject.tag == "Portions")
        {
            if (Speed < 10)
            {
                EEFManager.instance.GetAudio();
                Speed += 2;
                Destroy(other.gameObject);
            }
        }

        if(other.gameObject.tag == "HealthPortions")
        {
            if (currentHealth < maxHealth)
            {
                EEFManager.instance.GetAudio();
                currentHealth += 500f;
                if (currentHealth > maxHealth)
                {
                    currentHealth = maxHealth;
                }
                Destroy(other.gameObject);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.tag == "Doors")
        {
            if (haveKey)
            {
                BGMManager.instance.NormalBGM();
                EEFManager.instance.WinAudio();

                Destroy(collision.gameObject);

                win = true;
                WinCanvas.gameObject.SetActive(true);


                //string currentSceneName = SceneManager.GetActiveScene().name;

                // 在碰撞发生后的适当位置添加以下代码
                // 获取当前场景的序号
                //int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

                // 加载下一个场景
                //StartCoroutine(LoadSceneAfterDelay(4f, currentSceneIndex + 1));

                //返回开始场景
                StartCoroutine(LoadSceneAfterDelay(4f, "StartScene"));
            }
            else
            {
                EEFManager.instance.DeniedAudio();
                NoKeyCanvas.gameObject.SetActive(true);
                StartCoroutine(HideCanvasAfterDelay(2f, NoKeyCanvas));
            }
        }
        else if (collision.gameObject.tag == "Enemies" && collision.gameObject.GetComponent<Animator>().GetBool("Attack"))
        {
            currentHealth -= collision.gameObject.GetComponent<Enemy>().ATK;
            if(currentHealth <= 0) currentHealth = 0;
            EEFManager.instance.GameOverAudio();
            if(currentHealth <= 0)
            {
                GameOverCanvas.gameObject.SetActive(true);
                StartCoroutine(LoadSceneAfterDelay(4f, "StartScene"));
                Speed = 0;
            }
        }
    }

    //定时显示canvas
    private IEnumerator HideCanvasAfterDelay(float delay, Canvas canvas)
    {
        yield return new WaitForSeconds(delay);
        canvas.gameObject.SetActive(false);
    }

    // 在类中添加以下协程方法
    private IEnumerator LoadSceneAfterDelay(float delay, string sceneName)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator LoadSceneAfterDelay(float delay, int sceneName)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }

}
