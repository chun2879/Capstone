using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class RobotDataUIManager : MonoBehaviour
{
    [Header("Controller")]
    public PickAndPlaceController pickController;

    [Header("UI")]
    public Button playButton;
    public Button resetButton;
    public Slider timeSlider;
    public TMP_Text timeLabel;

    [Header("Joint Analysis UI")]
    public TMP_Dropdown jointDropdown;
    public GameObject jointGraphPanel;
    public JointGraphView jointGraphView;
    public TMP_Text jointTitleLabel;

    public bool IsPlaying = false;

    private bool ignoreSlider = false;
    private float simTime = 0f;

    private int currentJointIndex = 0;

    void Start()
    {
        playButton.onClick.AddListener(OnPlay);
        resetButton.onClick.AddListener(OnReset);
        timeSlider.onValueChanged.AddListener(OnSlider);

        timeSlider.minValue = 0f;
        timeSlider.maxValue = 0f;
        timeSlider.value = 0f;
        timeSlider.interactable = false;

        timeLabel.text = "0.00 s";

        // 관절 Dropdown 초기화
        if (jointDropdown != null && pickController != null && pickController.physicalArmJoints != null)
        {
            jointDropdown.ClearOptions();
            List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

            foreach (var j in pickController.physicalArmJoints)
            {
                options.Add(new TMP_Dropdown.OptionData(j.name));
            }

            jointDropdown.AddOptions(options);
            jointDropdown.onValueChanged.AddListener(OnJointDropdownChanged);
        }
    }

    void Update()
    {
        if (IsPlaying)
        {
            simTime += Time.deltaTime;

            ignoreSlider = true;
            timeSlider.value = simTime;
            ignoreSlider = false;

            timeLabel.text = simTime.ToString("F2") + " s";
        }

        // 그래프가 열려 있으면, 재생 중에도 하이라이트를 이동
        if (jointGraphView != null)
        {
            jointGraphView.SetHighlightTime(simTime);
        }
    }

    public void OnPlay()
    {
        if (pickController == null) return;

        simTime = 0f;
        IsPlaying = true;

        timeSlider.interactable = false;

        ignoreSlider = true;
        timeSlider.value = 0f;
        ignoreSlider = false;

        pickController.PlaySimulation();
    }

    public void OnReset()
    {
        IsPlaying = false;
        simTime = 0f;

        ignoreSlider = true;
        timeSlider.value = 0f;
        timeSlider.maxValue = 0f;
        timeSlider.interactable = false;
        ignoreSlider = false;

        timeLabel.text = "0.00 s";

        pickController.ResetSimulation();
    }

    public void OnSlider(float v)
    {
        if (ignoreSlider) return;

        if (pickController != null && pickController.recorder != null && pickController.recorder.HasData)
        {
            IsPlaying = false;

            simTime = v;
            pickController.ApplyRecordedState(v);

            timeLabel.text = v.ToString("F2") + " s";

            if (jointGraphPanel != null && jointGraphPanel.activeSelf && jointGraphView != null)
            {
                jointGraphView.SetHighlightTime(simTime);
            }
        }
    }

    public void OnSimulationFinished(float maxTime)
    {
        ignoreSlider = true;
        timeSlider.maxValue = maxTime;
        timeSlider.value = maxTime;
        timeSlider.interactable = true;
        ignoreSlider = false;

        IsPlaying = false;

        // 시뮬레이션 끝나자마자 그래프도 갱신
        RebuildJointGraph();
        if (jointGraphView != null)
            jointGraphView.SetHighlightTime(simTime);
    }

    // ====== Joint Graph UI API ======

    private void OnJointDropdownChanged(int index)
    {
        currentJointIndex = index;
        RebuildJointGraph();
    }


    private void RebuildJointGraph()
    {
        if (jointGraphView == null ||
            pickController == null ||
            pickController.recorder == null ||
            !pickController.recorder.HasData)
            return;

        var frames = pickController.recorder.Frames;
        if (frames == null || frames.Count == 0) return;

        List<float> times = new List<float>(frames.Count);
        List<float> torques = new List<float>(frames.Count);
        List<float> velocities = new List<float>(frames.Count);

        for (int i = 0; i < frames.Count; i++)
        {
            var f = frames[i];
            times.Add(f.time);

            float tVal = 0f;
            if (f.pseudoTorques != null &&
                currentJointIndex < f.pseudoTorques.Count)
                tVal = f.pseudoTorques[currentJointIndex];
            torques.Add(tVal);

            float vVal = 0f;
            if (f.jointVelocities != null &&
                currentJointIndex < f.jointVelocities.Count)
                vVal = f.jointVelocities[currentJointIndex];
            velocities.Add(vVal);
        }

        jointGraphView.SetData(times, torques, velocities);

        if (jointTitleLabel != null &&
            jointDropdown != null &&
            currentJointIndex < jointDropdown.options.Count)
        {
            jointTitleLabel.text = jointDropdown.options[currentJointIndex].text;
        }
    }
}
