using UnityEngine;

// �� ������Ʈ�� ���� ������ ���� ������ �����ϴ� ���Ҹ� �մϴ�.
public class GhostJointInfo : MonoBehaviour
{
    public Quaternion anchorRotation; // ArticulationBody�� Anchor Rotation ���� ������ ����
    public Vector3 axis;              // ���� ȸ�� �� (Revolute Joint�� �׻� X��)
    public float lowerLimit;          // �ּ� ȸ�� ���� (degrees)
    public float upperLimit;          // �ִ� ȸ�� ���� (degrees)
}