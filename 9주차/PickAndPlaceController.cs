using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PickAndPlaceController : MonoBehaviour
{
    public enum RobotState
    {
        IDLE,
        MOVING_TO_PRE_GRASP,
        LOWERING_TO_GRASP,
        GRASPING,
        LIFTING_CUBE,
        MOVING_TO_TARGET_PAD,
        LOWERING_TO_RELEASE,
        RELEASING,
        RETURNING_HOME
    }

    [Header("씬 객체 참조")]
    public Transform cube;
    public Transform targetPad;
    public Transform ikTarget;
    public Transform endEffector;

    [Header("로봇팔 관절 설정")]
    public List<Transform> ghostArmJoints;
    public List<ArticulationBody> physicalArmJoints;
    public List<ArticulationBody> gripperJoints;

    [Header("움직임 파라미터")]
    public float movementSpeed = 1.0f;
    public float safeHeightOffset = 0.2f;
    public float positionThreshold = 0.01f;

    [Header("그리퍼 회전 한계값 (도 단위)")]
    public float gripperUpperLimit = 30f;   // gripper1 열림 각도
    public float gripperLowerLimit = -30f;  // gripper2 열림 각도
    public float gripperSpeed = 60f;        // deg/sec

    private RobotState currentState;
    private Coroutine currentMovementCoroutine;
    private Vector3 initialIKTargetPosition;
    private List<Quaternion> initialJointRotations;
    private Transform heldObject = null;
    private Coroutine gripperCoroutine;

    void Start()
    {
        initialIKTargetPosition = ikTarget.position;

        // 초기 관절 회전 저장
        initialJointRotations = new List<Quaternion>();
        foreach (var joint in ghostArmJoints)
            initialJointRotations.Add(joint.localRotation);

        SetState(RobotState.IDLE);
    }

    void Update()
    {
        if (currentState == RobotState.IDLE && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartPickAndPlace();
        }
    }

    void LateUpdate()
    {
        if (physicalArmJoints.Count != ghostArmJoints.Count) return;

        for (int i = 0; i < physicalArmJoints.Count; i++)
        {
            Quaternion targetLocalRotation = ghostArmJoints[i].localRotation;
            var info = ghostArmJoints[i].GetComponent<GhostJointInfo>();
            if (info == null) continue;

            targetLocalRotation.ToAngleAxis(out float angle, out Vector3 axis);
            Vector3 jointAxis = info.anchorRotation * info.axis;
            float targetAngle = angle * Vector3.Dot(axis.normalized, jointAxis.normalized);

            var drive = physicalArmJoints[i].xDrive;
            drive.target = targetAngle;
            physicalArmJoints[i].xDrive = drive;
        }
    }

    public void StartPickAndPlace()
    {
        if (currentState == RobotState.IDLE)
            SetState(RobotState.MOVING_TO_PRE_GRASP);
    }

    private void SetState(RobotState newState)
    {
        currentState = newState;
        Debug.Log("상태 변경: " + newState);

        if (currentMovementCoroutine != null)
            StopCoroutine(currentMovementCoroutine);

        Vector3 approachOffset = transform.forward * 0.1f;

        switch (currentState)
        {
            case RobotState.IDLE:
                break;

            case RobotState.MOVING_TO_PRE_GRASP:
                StartGripperRotation(true); // 열기
                currentMovementCoroutine = StartCoroutine(MoveIKWithEasing(cube.position + Vector3.up * safeHeightOffset + approachOffset,
                    () => SetState(RobotState.LOWERING_TO_GRASP)));
                break;

            case RobotState.LOWERING_TO_GRASP:
                currentMovementCoroutine = StartCoroutine(MoveIKWithEasing(cube.position + approachOffset,
                    () => SetState(RobotState.GRASPING)));
                break;

            case RobotState.GRASPING:
                StartCoroutine(GraspSequence());
                break;

            case RobotState.LIFTING_CUBE:
                currentMovementCoroutine = StartCoroutine(MoveIKWithEasing(ikTarget.position + Vector3.up * safeHeightOffset,
                    () => SetState(RobotState.MOVING_TO_TARGET_PAD)));
                break;

            case RobotState.MOVING_TO_TARGET_PAD:
                currentMovementCoroutine = StartCoroutine(MoveIKWithEasing(targetPad.position + Vector3.up * safeHeightOffset + approachOffset,
                    () => SetState(RobotState.LOWERING_TO_RELEASE)));
                break;

            case RobotState.LOWERING_TO_RELEASE:
                currentMovementCoroutine = StartCoroutine(MoveIKWithEasing(targetPad.position + new Vector3(0, cube.localScale.y, 0) + approachOffset,
                    () => SetState(RobotState.RELEASING)));
                break;

            case RobotState.RELEASING:
                StartCoroutine(ReleaseSequence());
                break;

            case RobotState.RETURNING_HOME:
                // 큐브를 놓은 위치에서 safe height로 먼저 올린 후 홈으로 이동
                Vector3 liftedPosition = ikTarget.position + Vector3.up * safeHeightOffset;
                currentMovementCoroutine = StartCoroutine(MoveIKWithEasing(liftedPosition,
                    () => StartCoroutine(MoveIKAndGhostArm(initialIKTargetPosition, initialJointRotations, RobotState.IDLE))));
                break;
        }
    }

    // -----------------------------
    // Smooth IK 이동 (콜백 기반)
    // -----------------------------
    private IEnumerator MoveIKWithEasing(Vector3 targetPosition, System.Action onComplete)
    {
        Vector3 startPos = ikTarget.position;
        float distance = Vector3.Distance(startPos, targetPosition);
        float duration = distance / movementSpeed;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            t = Mathf.Clamp01(t);
            float easeT = t * t * (3f - 2f * t); // Smoothstep easing
            ikTarget.position = Vector3.Lerp(startPos, targetPosition, easeT);
            yield return null;
        }

        ikTarget.position = targetPosition;
        onComplete?.Invoke();
    }

    // -----------------------------
    // 그리퍼 easing 회전
    // -----------------------------
    private void StartGripperRotation(bool open)
    {
        if (gripperCoroutine != null)
            StopCoroutine(gripperCoroutine);
        gripperCoroutine = StartCoroutine(GripperRotationEasingCoroutine(open));
    }

    private IEnumerator GripperRotationEasingCoroutine(bool open)
    {
        if (gripperJoints.Count < 2) yield break;

        float target1 = open ? gripperUpperLimit : 0f;
        float target2 = open ? gripperLowerLimit : 0f;

        float start1 = gripperJoints[0].xDrive.target;
        float start2 = gripperJoints[1].xDrive.target;

        float maxAngleDiff = Mathf.Max(Mathf.Abs(start1 - target1), Mathf.Abs(start2 - target2));
        float duration = maxAngleDiff / gripperSpeed;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            t = Mathf.Clamp01(t);
            float easeT = t * t * (3f - 2f * t);

            var drive1 = gripperJoints[0].xDrive;
            drive1.target = Mathf.Lerp(start1, target1, easeT);
            gripperJoints[0].xDrive = drive1;

            var drive2 = gripperJoints[1].xDrive;
            drive2.target = Mathf.Lerp(start2, target2, easeT);
            gripperJoints[1].xDrive = drive2;

            yield return null;
        }
    }

    private IEnumerator GraspSequence()
    {
        StartGripperRotation(false); // 닫기
        yield return new WaitForSeconds(1.0f);
        GraspObject();
        yield return new WaitForSeconds(0.5f);
        SetState(RobotState.LIFTING_CUBE);
    }

    private IEnumerator ReleaseSequence()
    {
        ReleaseObject();
        yield return new WaitForSeconds(0.5f);
        StartGripperRotation(true); // 열기
        yield return new WaitForSeconds(1.0f);
        SetState(RobotState.RETURNING_HOME);
    }

    private void GraspObject()
    {
        if (heldObject == null && cube != null)
        {
            cube.GetComponent<Rigidbody>().isKinematic = true;
            cube.transform.SetParent(endEffector, true);
            heldObject = cube;
        }
    }

    private void ReleaseObject()
    {
        if (heldObject != null)
        {
            heldObject.transform.SetParent(null, true);
            heldObject.GetComponent<Rigidbody>().isKinematic = false;
            heldObject = null;
        }
    }

    // -----------------------------
    // IK + GhostArm 동시 이동 (홈 복귀)
    // -----------------------------
    private IEnumerator MoveIKAndGhostArm(Vector3 targetPosition, List<Quaternion> targetRotations, RobotState nextState)
    {
        Vector3 startPos = ikTarget.position;
        float distance = Vector3.Distance(startPos, targetPosition);
        float duration = distance / movementSpeed;
        float t = 0f;

        List<Quaternion> startRotations = new List<Quaternion>();
        foreach (var joint in ghostArmJoints)
            startRotations.Add(joint.localRotation);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            t = Mathf.Clamp01(t);
            float easeT = t * t * (3f - 2f * t);

            ikTarget.position = Vector3.Lerp(startPos, targetPosition, easeT);

            for (int i = 0; i < ghostArmJoints.Count; i++)
                ghostArmJoints[i].localRotation = Quaternion.Slerp(startRotations[i], targetRotations[i], easeT);

            yield return null;
        }

        ikTarget.position = targetPosition;
        for (int i = 0; i < ghostArmJoints.Count; i++)
            ghostArmJoints[i].localRotation = targetRotations[i];

        SetState(nextState);
    }
}
