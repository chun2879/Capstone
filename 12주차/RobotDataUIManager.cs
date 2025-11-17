using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RobotDataUIManager : MonoBehaviour
{
    [Header("Controller")]
    public PickAndPlaceController pickController;

    [Header("UI")]
    public Button playButton;
    public Button resetButton;
    public Slider timeSlider;
    public TMP_Text timeLabel;

    public bool IsPlaying = false;

    private bool ignoreSlider = false;
    private float simTime = 0f;

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
    }

    void Update()
    {
        if (!IsPlaying) return;

        simTime += Time.deltaTime;

        ignoreSlider = true;
        timeSlider.value = simTime;
        ignoreSlider = false;

        timeLabel.text = simTime.ToString("F2") + " s";
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
    }
}
