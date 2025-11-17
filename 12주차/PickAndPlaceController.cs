using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
        RETURNING_HOME,
        REPLAY
    }

    [Header("Scene References")]
    public Transform cube;
    public Transform targetPad;
    public Transform ikTarget;
    public Transform endEffector;

    [Header("Joints")]
    public List<Transform> ghostArmJoints;
    public List<ArticulationBody> physicalArmJoints;
    public List<ArticulationBody> gripperJoints;

    [Header("Movement Settings")]
    public float movementSpeed = 1f;
    public float safeHeightOffset = 0.2f;

    [Header("Gripper Settings")]
    public float gripperUpperLimit = 30f;
    public float gripperLowerLimit = -30f;
    public float gripperSpeed = 60f;

    [Header("UI")]
    public RobotDataUIManager ui;

    [Header("Recorder")]
    public StateRecorder recorder;

    [Header("Heatmap")]
    public PseudoTorqueHeatmap heatmap;

    private RobotState currentState;
    private Coroutine currentMove;
    private Coroutine gripMove;

    private Vector3 initialIKPos;
    private List<Quaternion> initialGhostRot;

    private Vector3 cubeStartPos;
    private Quaternion cubeStartRot;

    private float simTime = 0f;
    private Transform heldObject = null;

    public bool IsReplaying => currentState == RobotState.REPLAY;

    void Start()
    {
        initialIKPos = ikTarget.position;

        initialGhostRot = new List<Quaternion>();
        foreach (var j in ghostArmJoints)
            initialGhostRot.Add(j.localRotation);

        if (cube != null)
        {
            cubeStartPos = cube.position;
            cubeStartRot = cube.rotation;
        }

        SetState(RobotState.IDLE);
    }

    void Update()
    {
        if (currentState == RobotState.REPLAY) return;

        if (ui != null && ui.IsPlaying && recorder != null)
        {
            simTime += Time.deltaTime;

            recorder.RecordFrame(
                simTime,
                ikTarget,
                ghostArmJoints,
                cube,
                heldObject != null,
                heatmap
            );
        }
    }

    void LateUpdate()
    {
        if (physicalArmJoints.Count != ghostArmJoints.Count) return;

        for (int i = 0; i < physicalArmJoints.Count; i++)
        {
            var info = ghostArmJoints[i].GetComponent<GhostJointInfo>();
            if (info == null) continue;

            Quaternion targetLocalRotation = ghostArmJoints[i].localRotation;
            targetLocalRotation.ToAngleAxis(out float angle, out Vector3 axis);

            Vector3 jointAxis = info.anchorRotation * info.axis;
            float targetAngle = angle * Vector3.Dot(axis.normalized, jointAxis.normalized);

            var drive = physicalArmJoints[i].xDrive;
            drive.target = targetAngle;
            physicalArmJoints[i].xDrive = drive;
        }
    }

    // ================== UI API ==================
    public void PlaySimulation()
    {
        if (currentState == RobotState.IDLE)
        {
            if (recorder != null) recorder.Clear();
            simTime = 0f;

            SetState(RobotState.MOVING_TO_PRE_GRASP);
        }
    }

    public void ResetSimulation()
    {
        if (currentMove != null) StopCoroutine(currentMove);
        if (gripMove != null) StopCoroutine(gripMove);

        if (recorder != null) recorder.Clear();
        simTime = 0f;

        ikTarget.position = initialIKPos;

        for (int i = 0; i < ghostArmJoints.Count; i++)
            ghostArmJoints[i].localRotation = initialGhostRot[i];

        if (cube != null)
        {
            cube.SetParent(null, true);
            var rb = cube.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            cube.position = cubeStartPos;
            cube.rotation = cubeStartRot;
        }

        heldObject = null;

        currentState = RobotState.IDLE;
    }

    public void ApplyRecordedState(float time)
    {
        if (recorder == null || !recorder.HasData) return;

        var f = recorder.GetFrameAtTime(time);
        if (f == null) return;

        currentState = RobotState.REPLAY;

        // IK
        ikTarget.position = f.ikPosition;

        // Ghost joints
        int n = Mathf.Min(f.ghostRotations.Count, ghostArmJoints.Count);
        for (int i = 0; i < n; i++)
            ghostArmJoints[i].localRotation = f.ghostRotations[i];

        // Cube
        if (cube != null)
        {
            var rb = cube.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true; // scrubbing 중 물리 off

            cube.position = f.cubePosition;
            cube.rotation = f.cubeRotation;

            if (f.cubeHeld)
                cube.SetParent(endEffector, true);
            else
                cube.SetParent(null, true);
        }

        // 히트맵 토크 스냅샷 적용
        if (heatmap != null)
            heatmap.ApplyTorqueSnapshot(f.pseudoTorques);
    }

    // ================== FSM ==================
    private void SetState(RobotState newState)
    {
        currentState = newState;
        if (currentMove != null) StopCoroutine(currentMove);

        Vector3 offset = transform.forward * 0.1f;

        switch (newState)
        {
            case RobotState.IDLE:
                if (ui != null && recorder != null)
                    ui.OnSimulationFinished(recorder.MaxTime);
                break;

            case RobotState.MOVING_TO_PRE_GRASP:
                StartGripperRotation(true);
                currentMove = StartCoroutine(MoveIKWithEasing(
                    cube.position + Vector3.up * safeHeightOffset + offset,
                    () => SetState(RobotState.LOWERING_TO_GRASP)
                ));
                break;

            case RobotState.LOWERING_TO_GRASP:
                currentMove = StartCoroutine(MoveIKWithEasing(
                    cube.position + offset,
                    () => SetState(RobotState.GRASPING)
                ));
                break;

            case RobotState.GRASPING:
                StartCoroutine(GraspSequence());
                break;

            case RobotState.LIFTING_CUBE:
                currentMove = StartCoroutine(MoveIKWithEasing(
                    ikTarget.position + Vector3.up * safeHeightOffset,
                    () => SetState(RobotState.MOVING_TO_TARGET_PAD)
                ));
                break;

            case RobotState.MOVING_TO_TARGET_PAD:
                currentMove = StartCoroutine(MoveIKWithEasing(
                    targetPad.position + Vector3.up * safeHeightOffset + offset,
                    () => SetState(RobotState.LOWERING_TO_RELEASE)
                ));
                break;

            case RobotState.LOWERING_TO_RELEASE:
                currentMove = StartCoroutine(MoveIKWithEasing(
                    targetPad.position + new Vector3(0, cube.localScale.y, 0) + offset,
                    () => SetState(RobotState.RELEASING)
                ));
                break;

            case RobotState.RELEASING:
                StartCoroutine(ReleaseSequence());
                break;

            case RobotState.RETURNING_HOME:
                Vector3 lifted = ikTarget.position + Vector3.up * safeHeightOffset;
                currentMove = StartCoroutine(MoveIKWithEasing(
                    lifted,
                    () => StartCoroutine(MoveIKAndGhostArm(
                        initialIKPos,
                        initialGhostRot,
                        RobotState.IDLE
                    ))
                ));
                break;
        }
    }

    // ================== Coroutines ==================
    private IEnumerator MoveIKWithEasing(Vector3 target, System.Action onComplete)
    {
        Vector3 start = ikTarget.position;
        float dist = Vector3.Distance(start, target);
        float duration = Mathf.Max(0.0001f, dist / movementSpeed);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float ease = t * t * (3f - 2f * t);
            ikTarget.position = Vector3.Lerp(start, target, ease);
            yield return null;
        }

        ikTarget.position = target;
        onComplete?.Invoke();
    }

    private void StartGripperRotation(bool open)
    {
        if (gripMove != null) StopCoroutine(gripMove);
        gripMove = StartCoroutine(GripperRotationEasing(open));
    }

    private IEnumerator GripperRotationEasing(bool open)
    {
        if (gripperJoints.Count < 2) yield break;

        float targ1 = open ? gripperUpperLimit : 0f;
        float targ2 = open ? gripperLowerLimit : 0f;

        float s1 = gripperJoints[0].xDrive.target;
        float s2 = gripperJoints[1].xDrive.target;

        float diff = Mathf.Max(Mathf.Abs(s1 - targ1), Mathf.Abs(s2 - targ2));
        float dur = Mathf.Max(0.0001f, diff / gripperSpeed);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float ease = t * t * (3f - 2f * t);

            var d1 = gripperJoints[0].xDrive;
            d1.target = Mathf.Lerp(s1, targ1, ease);
            gripperJoints[0].xDrive = d1;

            var d2 = gripperJoints[1].xDrive;
            d2.target = Mathf.Lerp(s2, targ2, ease);
            gripperJoints[1].xDrive = d2;

            yield return null;
        }
    }

    private IEnumerator GraspSequence()
    {
        StartGripperRotation(false);
        yield return new WaitForSeconds(1f);

        if (cube != null)
        {
            var rb = cube.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            cube.SetParent(endEffector, true);
            heldObject = cube;
        }

        yield return new WaitForSeconds(0.5f);
        SetState(RobotState.LIFTING_CUBE);
    }

    private IEnumerator ReleaseSequence()
    {
        if (cube != null)
        {
            cube.SetParent(null, true);
            var rb = cube.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;
        }
        heldObject = null;

        yield return new WaitForSeconds(0.5f);
        StartGripperRotation(true);
        yield return new WaitForSeconds(1f);

        SetState(RobotState.RETURNING_HOME);
    }

    private IEnumerator MoveIKAndGhostArm(Vector3 targetPos, List<Quaternion> targetRot, RobotState nextState)
    {
        Vector3 start = ikTarget.position;
        float dist = Vector3.Distance(start, targetPos);
        float duration = Mathf.Max(0.0001f, dist / movementSpeed);
        float t = 0f;

        List<Quaternion> startRot = new List<Quaternion>();
        foreach (var j in ghostArmJoints)
            startRot.Add(j.localRotation);

        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float ease = t * t * (3f - 2f * t);

            ikTarget.position = Vector3.Lerp(start, targetPos, ease);

            for (int i = 0; i < ghostArmJoints.Count; i++)
                ghostArmJoints[i].localRotation = Quaternion.Slerp(startRot[i], targetRot[i], ease);

            yield return null;
        }

        ikTarget.position = targetPos;
        for (int i = 0; i < ghostArmJoints.Count; i++)
            ghostArmJoints[i].localRotation = targetRot[i];

        SetState(nextState);
    }
}
