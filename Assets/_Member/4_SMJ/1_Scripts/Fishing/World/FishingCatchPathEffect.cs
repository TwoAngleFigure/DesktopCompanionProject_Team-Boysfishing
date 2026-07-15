using System;
using DG.Tweening;
using UnityEngine;

public class FishingCatchPathEffect : MonoBehaviour
{
    [Header("Path Points")]
    [SerializeField] private Transform m_startPoint;
    [SerializeField] private Transform[] m_pathPoints;
    [SerializeField] private Transform m_endPoint;

    [Header("Path Settings")]
    [SerializeField] private float m_duration = 1.5f;

    [SerializeField, Min(1)]
    private int m_resolution = 10;

    [Header("Gizmos")]
    [SerializeField] private Color m_gizmoPathColor = Color.cyan;

    [SerializeField, Min(0f)]
    private float m_gizmoPointRadius = 0.05f;

    private Tween m_pathTween;

    public bool Play(Transform target, Action onComplete = null)
    {
        if (target == null)
        {
            return false;
        }

        if (!TryCreatePath(target, out Vector3[] path))
        {
            return false;
        }

        Stop();

        target.localPosition =
            ConvertToTargetLocalPosition(
                target,
                m_startPoint.position);

        Tween createdTween = null;

        createdTween = target
            .DOLocalPath(
                path,
                m_duration,
                PathType.CatmullRom,
                PathMode.Full3D,
                m_resolution)
            .SetEase(Ease.Linear)
            .SetLink(target.gameObject)
            .OnComplete(() =>
            {
                if (m_pathTween == createdTween)
                {
                    m_pathTween = null;
                }

                onComplete?.Invoke();
            })
            .OnKill(() =>
            {
                if (m_pathTween == createdTween)
                {
                    m_pathTween = null;
                }
            });

        m_pathTween = createdTween;
        return true;
    }

    public void Stop()
    {
        Tween tween = m_pathTween;
        m_pathTween = null;
        tween?.Kill();
    }

    private bool TryCreatePath(Transform target, out Vector3[] path)
    {
        path = null;

        if (m_startPoint == null)
        {
            Debug.LogWarning("[FishingCatchPathEffect] Start Point가 없습니다.");
            return false;
        }

        if (m_endPoint == null)
        {
            Debug.LogWarning("[FishingCatchPathEffect] End Point가 없습니다.");
            return false;
        }

        int middlePointCount = m_pathPoints != null ? m_pathPoints.Length : 0;

        path = new Vector3[middlePointCount + 1];

        for (int i = 0; i < middlePointCount; i++)
        {
            Transform pathPoint = m_pathPoints[i];

            if (pathPoint == null)
            {
                Debug.LogWarning($"[FishingCatchPathEffect] Path Point {i}가 비어 있습니다.");

                return false;
            }

            path[i] = ConvertToTargetLocalPosition(target, pathPoint.position);
        }

        path[path.Length - 1] =
            ConvertToTargetLocalPosition(target, m_endPoint.position);

        return true;
    }

    private Vector3 ConvertToTargetLocalPosition(
    Transform target,
    Vector3 worldPosition)
    {
        Transform targetParent = target.parent;

        if (targetParent == null)
        {
            return worldPosition;
        }

        return targetParent.InverseTransformPoint(worldPosition);
    }

    private void OnDisable()
    {
        Stop();
    }

    #region Gizmos
    private void OnDrawGizmos()
    {
        if (!TryCreateGizmoPath(out Vector3[] points))
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = m_gizmoPathColor;

        int samplesPerSegment = Mathf.Max(2, m_resolution);

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 p1 = points[i];
            Vector3 p2 = points[i + 1];
            Vector3 p0 = i > 0
                ? points[i - 1]
                : p1 + (p1 - p2);
            Vector3 p3 = i + 2 < points.Length
                ? points[i + 2]
                : p2 + (p2 - p1);

            Vector3 previousPoint = p1;

            for (int sample = 1; sample <= samplesPerSegment; sample++)
            {
                float t = sample / (float)samplesPerSegment;
                Vector3 currentPoint = EvaluateCatmullRom(
                    p0,
                    p1,
                    p2,
                    p3,
                    t);

                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        DrawGizmoPoint(points[0], Color.green);

        for (int i = 1; i < points.Length - 1; i++)
        {
            DrawGizmoPoint(points[i], Color.yellow);
        }

        DrawGizmoPoint(points[points.Length - 1], Color.red);
        Gizmos.color = previousColor;
    }

    private bool TryCreateGizmoPath(out Vector3[] points)
    {
        points = null;

        if (m_startPoint == null || m_endPoint == null)
        {
            return false;
        }

        int middlePointCount = m_pathPoints != null
            ? m_pathPoints.Length
            : 0;

        points = new Vector3[middlePointCount + 2];
        points[0] = m_startPoint.position;

        for (int i = 0; i < middlePointCount; i++)
        {
            if (m_pathPoints[i] == null)
            {
                points = null;
                return false;
            }

            points[i + 1] = m_pathPoints[i].position;
        }

        points[points.Length - 1] = m_endPoint.position;
        return true;
    }

    private void DrawGizmoPoint(Vector3 position, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireSphere(position, m_gizmoPointRadius);
        Gizmos.color = m_gizmoPathColor;
    }

    private static Vector3 EvaluateCatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f *
            ((2f * p1) +
             (-p0 + p2) * t +
             (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
             (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    #endregion
}
