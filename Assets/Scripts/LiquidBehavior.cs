using UnityEngine;
 
[ExecuteInEditMode]
public class LiquidBehavior : MonoBehaviour
{
    public enum UpdateMode { Normal, UnscaledTime }
    public UpdateMode updateMode;
 
    [SerializeField] 
    private float MaxWobble = 0.03f;
    [SerializeField] 
    private float WobbleSpeedMove = 1f;
    [SerializeField] 
    private float fillAmount = 0.5f;
    [SerializeField] 
    private float Recovery = 1f;
    [SerializeField] 
    private float Thickness = 1f;
    [Range(0, 1)]
    public float CompensateShapeAmount;
    [SerializeField] 
    private Mesh mesh;
    [SerializeField] 
    private Renderer rend;

    private Vector3 m_Pos;
    private Vector3 m_LastPos;
    private Vector3 m_Velocity;
    private Quaternion m_LastRot;
    private Vector3 m_AngularVelocity;
    private float m_WobbleAmountX;
    private float m_WobbleAmountZ;
    private float m_WobbleAmountToAddX;
    private float m_WobbleAmountToAddZ;
    private float m_Pulse;
    private float m_SineWave;
    private float m_Time = 0.5f;
    private Vector3 m_Comp;

    private void Start()
    {
        GetMeshAndRend();
    }
 
    private void OnValidate()
    {
        GetMeshAndRend();
    }

    private void GetMeshAndRend()
    {
        if (mesh == null)
        {
            mesh = GetComponent<MeshFilter>().sharedMesh;
        }
        if (rend == null)
        {
            rend = GetComponent<Renderer>();
        }
    }

    private void Update()
    {
        float deltaTime = 0;
        switch (updateMode)
        {
            case UpdateMode.Normal:
                deltaTime = Time.deltaTime;
                break;
 
            case UpdateMode.UnscaledTime:
                deltaTime = Time.unscaledDeltaTime;
                break;
        }
 
        m_Time += deltaTime;
 
        if (deltaTime != 0)
        {
 
 
            // decrease wobble over time
            m_WobbleAmountToAddX = Mathf.Lerp(m_WobbleAmountToAddX, 0, (deltaTime * Recovery));
            m_WobbleAmountToAddZ = Mathf.Lerp(m_WobbleAmountToAddZ, 0, (deltaTime * Recovery));
 
 
 
            // make a sine wave of the decreasing wobble
            m_Pulse = 2 * Mathf.PI * WobbleSpeedMove;
            m_SineWave = Mathf.Lerp(m_SineWave, Mathf.Sin(m_Pulse * m_Time), deltaTime * Mathf.Clamp(m_Velocity.magnitude + m_AngularVelocity.magnitude, Thickness, 10));
 
            m_WobbleAmountX = m_WobbleAmountToAddX * m_SineWave;
            m_WobbleAmountZ = m_WobbleAmountToAddZ * m_SineWave;
 
 
 
            // velocity
            m_Velocity = (m_LastPos - transform.position) / deltaTime;
 
            m_AngularVelocity = GetAngularVelocity(m_LastRot, transform.rotation);
 
            // add clamped velocity to wobble
            m_WobbleAmountToAddX += Mathf.Clamp((m_Velocity.x + (m_Velocity.y * 0.2f) + m_AngularVelocity.z + m_AngularVelocity.y) * MaxWobble, -MaxWobble, MaxWobble);
            m_WobbleAmountToAddZ += Mathf.Clamp((m_Velocity.z + (m_Velocity.y * 0.2f) + m_AngularVelocity.x + m_AngularVelocity.y) * MaxWobble, -MaxWobble, MaxWobble);
        }
 
        // send it to the shader
        rend.sharedMaterial.SetFloat("_WobbleX", m_WobbleAmountX);
        rend.sharedMaterial.SetFloat("_WobbleZ", m_WobbleAmountZ);
 
        // set fill amount
        UpdatePos(deltaTime);
 
        // keep last position
        m_LastPos = transform.position;
        m_LastRot = transform.rotation;
    }

    private void UpdatePos(float deltaTime)
    {
 
        Vector3 worldPos = transform.TransformPoint(new Vector3(mesh.bounds.center.x, mesh.bounds.center.y, mesh.bounds.center.z));
        if (CompensateShapeAmount > 0)
        {
            // only lerp if not paused/normal update
            if (deltaTime != 0)
            {
                m_Comp = Vector3.Lerp(m_Comp, (worldPos - new Vector3(0, GetLowestPoint(), 0)), deltaTime * 10);
            }
            else
            {
                m_Comp = (worldPos - new Vector3(0, GetLowestPoint(), 0));
            }
 
            m_Pos = worldPos - transform.position - new Vector3(0, fillAmount - (m_Comp.y * CompensateShapeAmount), 0);
        }
        else
        {
            m_Pos = worldPos - transform.position - new Vector3(0, fillAmount, 0);
        }
        rend.sharedMaterial.SetVector("_FillAmount", m_Pos);
    }
 
    //https://forum.unity.com/threads/manually-calculate-angular-velocity-of-gameobject.289462/#post-4302796
    private Vector3 GetAngularVelocity(Quaternion foreLastFrameRotation, Quaternion lastFrameRotation)
    {
        var q = lastFrameRotation * Quaternion.Inverse(foreLastFrameRotation);
        // no rotation?
        // You may want to increase this closer to 1 if you want to handle very small rotations.
        // Beware, if it is too close to one your answer will be Nan
        if (Mathf.Abs(q.w) > 1023.5f / 1024.0f)
            return Vector3.zero;
        float gain;
        // handle negatives, we could just flip it but this is faster
        if (q.w < 0.0f)
        {
            var angle = Mathf.Acos(-q.w);
            gain = -2.0f * angle / (Mathf.Sin(angle) * Time.deltaTime);
        }
        else
        {
            var angle = Mathf.Acos(q.w);
            gain = 2.0f * angle / (Mathf.Sin(angle) * Time.deltaTime);
        }
        Vector3 angularVelocity = new Vector3(q.x * gain, q.y * gain, q.z * gain);
 
        if (float.IsNaN(angularVelocity.z))
        {
            angularVelocity = Vector3.zero;
        }
        return angularVelocity;
    }

    private float GetLowestPoint()
    {
        float lowestY = float.MaxValue;
        Vector3 lowestVert = Vector3.zero;
        Vector3[] vertices = mesh.vertices;
 
        for (int i = 0; i < vertices.Length; i++)
        {
 
            Vector3 position = transform.TransformPoint(vertices[i]);
 
            if (position.y < lowestY)
            {
                lowestY = position.y;
                lowestVert = position;
            }
        }
        return lowestVert.y;
    }
}