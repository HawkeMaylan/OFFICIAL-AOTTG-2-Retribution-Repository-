using UnityEngine;
using Photon.Pun;
using Settings;
using GameManagers;
using ApplicationManagers;
using System.Collections;
using Characters;
using UI;

public class AttachToHorseTrigger : MonoBehaviourPunCallbacks
{
    [Header("Attachment Offset")]
    public Vector3 attachOffset = new Vector3(0f, 0f, -2f);

    [Header("Joint Motion Limits")]
    public float linearLimit = 2f;

    [Header("Linear Drive Settings")]
    public float linearSpring = 50f;
    public float linearDamper = 20f;

    [Header("Angular Drive Settings")]
    public float angularSpring = 0f;
    public float angularDamper = 0f;

    [Header("Joint Break Settings")]
    public float jointBreakForce = Mathf.Infinity;
    public float jointBreakTorque = Mathf.Infinity;
    public bool enableCollision = false;

    [Header("Horse Turn Constraint")]
    public float maxAllowedHorseTurnAngle = 45f;
    public float autoDetachRange = 10f;

    private bool isAttached = false;
    private Transform horseRootInContact;
    private Transform attachedHorse;
    private Rigidbody rb;
    private Transform wagon;
    private PhotonView pv;
    private int attachedHorseViewID = -1;

    private string currentPrompt = "";
    private Coroutine detachCheckCoroutine;
    private Coroutine attachPromptCoroutine;
    private GUIStyle _promptStyle;
    private bool _isApplicationQuitting = false;

    private void Start()
    {
        wagon = transform.root;
        rb = wagon.GetComponent<Rigidbody>();
        pv = wagon.GetComponent<PhotonView>();
        ClearPrompt();
    }

    private void OnApplicationQuit()
    {
        _isApplicationQuitting = true;
    }

    private void OnDestroy()
    {
        if (_isApplicationQuitting) return;

        // Clean up coroutines
        if (detachCheckCoroutine != null)
            StopCoroutine(detachCheckCoroutine);

        if (attachPromptCoroutine != null)
            StopCoroutine(attachPromptCoroutine);
    }

    private void Update()
    {
        if (ChatManager.IsChatActive()) return;

        // Validate horseRootInContact is still valid
        if (horseRootInContact != null && horseRootInContact.gameObject == null)
        {
            horseRootInContact = null;
            ClearPrompt();
        }

        if (SettingsManager.InputSettings.Interaction.Interact2.GetKeyDown() && !InGameMenu.InMenu() && !ChatManager.IsChatActive())
        {
            if (!isAttached && horseRootInContact != null)
            {
                PhotonView horseView = horseRootInContact.GetComponentInParent<PhotonView>();
                Horse horseComponent = horseRootInContact.GetComponentInParent<Horse>();

                if (horseView != null && horseComponent != null && horseView.Owner == PhotonNetwork.LocalPlayer)
                {
                    if (horseComponent.MountedStatus == 1)
                    {
                        if (!pv.IsMine)
                            pv.RequestOwnership();

                        pv.RPC("RPC_AttachToHorse", RpcTarget.AllBuffered, horseView.ViewID, attachOffset);
                    }
                }
            }
            else if (isAttached && pv.IsMine)
            {
                if (attachedHorse != null)
                {
                    Horse horseComponent = attachedHorse.GetComponentInParent<Horse>();
                    if (horseComponent != null && horseComponent.MountedStatus == 1)
                    {
                        pv.RPC("RPC_DetachFromHorse", RpcTarget.AllBuffered);
                    }
                }
            }
        }
    }

    public bool IsAttachedToThisHorse(Component horse)
    {
        return isAttached && attachedHorse == horse.transform;
    }

    public bool IsHorseRotationAllowed(Transform horse)
    {
        if (!isAttached || attachedHorse != horse) return true;

        float angle = Vector3.Angle(wagon.forward, horse.forward);
        return angle <= maxAllowedHorseTurnAngle;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isAttached) return;
        if (!pv.IsMine || isAttached) return;
        if (other.name == "HorseTrigger")
        {
            Transform horseRoot = other.transform.root;
            PhotonView horseView = horseRoot.GetComponentInParent<PhotonView>();
            Horse horseComponent = horseRoot.GetComponentInParent<Horse>();

            if (horseView != null && horseComponent != null &&
                horseView.Owner == PhotonNetwork.LocalPlayer && horseComponent.MountedStatus == 1)
            {
                horseRootInContact = horseRoot;
                SetPrompt($"Press {SettingsManager.InputSettings.Interaction.Interact2.ToString()} to Attach", 3f);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!pv.IsMine || isAttached) return;
        if (other.name == "HorseTrigger" && other.transform.root == horseRootInContact)
        {
            horseRootInContact = null;
            ClearPrompt();
        }
    }

    [PunRPC]
    private void RPC_AttachToHorse(int horseViewID, Vector3 offset)
    {
        PhotonView horseView = PhotonView.Find(horseViewID);
        if (horseView == null) return;

        Transform horseRoot = horseView.transform;

        wagon.position = horseRoot.TransformPoint(offset);
        wagon.rotation = horseRoot.rotation;

        var existingJoint = wagon.GetComponent<ConfigurableJoint>();
        if (existingJoint != null)
            Destroy(existingJoint);

        ConfigurableJoint joint = wagon.gameObject.AddComponent<ConfigurableJoint>();
        Rigidbody horseRb = horseRoot.GetComponent<Rigidbody>();
        if (horseRb != null)
            joint.connectedBody = horseRb;

        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;

        SoftJointLimit limit = new SoftJointLimit { limit = linearLimit };
        joint.linearLimit = limit;

        JointDrive linearDrive = new JointDrive
        {
            positionSpring = linearSpring,
            positionDamper = linearDamper,
            maximumForce = Mathf.Infinity
        };
        joint.xDrive = joint.yDrive = joint.zDrive = linearDrive;

        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.slerpDrive = new JointDrive
        {
            positionSpring = angularSpring,
            positionDamper = angularDamper,
            maximumForce = Mathf.Infinity
        };

        joint.breakForce = jointBreakForce;
        joint.breakTorque = jointBreakTorque;
        joint.enableCollision = enableCollision;

        rb.isKinematic = false;
        isAttached = true;
        attachedHorse = horseRoot;
        attachedHorseViewID = horseViewID;

        SetPrompt($"Press {SettingsManager.InputSettings.Interaction.Interact2.ToString()} to Detach", 3f);

        if (detachCheckCoroutine != null) StopCoroutine(detachCheckCoroutine);
        detachCheckCoroutine = StartCoroutine(AutoDetachCheck());
    }

    [PunRPC]
    private void RPC_DetachFromHorse()
    {
        var joint = wagon.GetComponent<ConfigurableJoint>();
        if (joint != null)
            Destroy(joint);

        rb.isKinematic = false;
        isAttached = false;
        attachedHorse = null;
        attachedHorseViewID = -1;

        if (detachCheckCoroutine != null)
        {
            StopCoroutine(detachCheckCoroutine);
            detachCheckCoroutine = null;
        }

        ClearPrompt();
    }

    private IEnumerator AutoDetachCheck()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
        while (isAttached)
        {
            if (attachedHorse == null || Vector3.Distance(wagon.position, attachedHorse.position) > autoDetachRange)
            {
                if (pv.IsMine)
                    pv.RPC("RPC_DetachFromHorse", RpcTarget.AllBuffered);
                yield break;
            }

            float angle = Vector3.Angle(wagon.forward, attachedHorse.forward);
            if (angle > maxAllowedHorseTurnAngle)
            {
                if (pv.IsMine)
                    pv.RPC("RPC_DetachFromHorse", RpcTarget.AllBuffered);
                yield break;
            }

            yield return wait;
        }
    }

    private void SetPrompt(string text, float duration)
    {
        currentPrompt = text;
        if (attachPromptCoroutine != null) StopCoroutine(attachPromptCoroutine);
        attachPromptCoroutine = StartCoroutine(ClearPromptAfterDelay(duration));
    }

    private IEnumerator ClearPromptAfterDelay(float time)
    {
        yield return new WaitForSeconds(time);
        ClearPrompt();
        attachPromptCoroutine = null;
    }

    private void ClearPrompt()
    {
        currentPrompt = "";
        if (attachPromptCoroutine != null)
        {
            StopCoroutine(attachPromptCoroutine);
            attachPromptCoroutine = null;
        }
    }

    private void OnGUI()
    {
        if (!pv.IsMine || string.IsNullOrEmpty(currentPrompt))
            return;

        if (_promptStyle == null)
        {
            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };
        }

        GUI.Label(new Rect(Screen.width / 2 - 150, 50, 300, 50), currentPrompt, _promptStyle);
    }
}