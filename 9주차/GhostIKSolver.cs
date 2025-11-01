using UnityEngine;
using System.Collections.Generic;

public class GhostIKSolver : MonoBehaviour
{
    public Transform target;
    public List<Transform> ghostLinks; // link1부터 link6까지 할당
    public int iterations = 15;
    public float tolerance = 0.01f;

    void LateUpdate()
    {
        SolveIK();
    }

    void SolveIK()
    {
        if (target == null || ghostLinks == null || ghostLinks.Count < 2) return;

        Transform endEffector = ghostLinks[ghostLinks.Count - 1];

        for (int it = 0; it < iterations; it++)
        {
            if (Vector3.Distance(endEffector.position, target.position) < tolerance)
                break;

            // 끝에서 두 번째 링크부터 루트 방향으로 순회
            for (int i = ghostLinks.Count - 2; i >= 0; i--)
            {
                Transform currentLink = ghostLinks[i];
                var info = currentLink.GetComponent<GhostJointInfo>();
                if (info == null) continue;

                // 1. 각 관절의 실제 회전축을 월드 공간 기준으로 계산
                //    관절의 현재 회전(currentLink.rotation)과 복사해온 AnchorRotation을 모두 적용
                Vector3 worldAxis = currentLink.rotation * info.anchorRotation * info.axis;

                // 2. 벡터 계산
                Vector3 toEnd = endEffector.position - currentLink.position;
                Vector3 toTarget = target.position - currentLink.position;

                // 3. 두 벡터를 실제 회전축에 수직인 평면에 투영
                Vector3 projectedToEnd = Vector3.ProjectOnPlane(toEnd, worldAxis);
                Vector3 projectedToTarget = Vector3.ProjectOnPlane(toTarget, worldAxis);

                // 4. 투영된 벡터들 사이의 부호 있는 각도를 'worldAxis'를 기준으로 계산
                float signedAngle = Vector3.SignedAngle(projectedToEnd, projectedToTarget, worldAxis);

                // 5. 계산된 각도만큼 'worldAxis'를 중심으로 관절을 회전
                currentLink.rotation = Quaternion.AngleAxis(signedAngle, worldAxis) * currentLink.rotation;
            }
        }
    }
}