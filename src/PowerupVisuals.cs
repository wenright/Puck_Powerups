using UnityEngine;

namespace Powerups;

public sealed class PowerupVisualInstance : MonoBehaviour
{
    private static Material sharedParticleMaterial;
    private static Material sharedLineMaterial;

    private ulong ownerClientId;
    private string powerupName;
    private Color color;
    private float createdAt;
    private float fallbackDestroyAt;
    private float spinSpeed;
    private float ringRadius;
    private bool verticalRing;

    private Player player;
    private PlayerBody playerBody;
    private ParticleSystem particles;
    private LineRenderer line;
    private TrailRenderer trail;
    private Light glow;

    public string PowerupName => powerupName;

    public void Initialize(ulong ownerId, string name, float duration)
    {
        ownerClientId = ownerId;
        powerupName = name;
        color = PowerupPalette.Get(name);
        createdAt = Time.unscaledTime;
        fallbackDestroyAt = createdAt + Mathf.Max(1.25f, duration + 0.75f);

        ResolvePlayer();
        ConfigureEffect();
    }

    private void Update()
    {
        if (Time.unscaledTime >= fallbackDestroyAt)
        {
            Destroy(gameObject);
            return;
        }

        if (!playerBody) ResolvePlayer();
        if (!playerBody) return;

        Vector3 anchor = GetAnchorPosition();
        transform.position = anchor;

        float age = Time.unscaledTime - createdAt;
        if (powerupName == PowerupNames.Jetpack)
        {
            transform.rotation = Quaternion.LookRotation(-playerBody.transform.up, playerBody.transform.forward);
        }
        else if (powerupName == PowerupNames.Turbo)
        {
            transform.rotation = Quaternion.LookRotation(-playerBody.transform.forward, playerBody.transform.up);
        }
        else if (spinSpeed != 0f)
        {
            transform.rotation = Quaternion.AngleAxis(age * spinSpeed, Vector3.up);
        }

        if (glow)
        {
            glow.intensity = 1.3f + Mathf.Sin(Time.unscaledTime * 7f) * 0.45f;
        }

        UpdateLine();
        UpdateRing();

        if (powerupName == PowerupNames.Grapple && Time.unscaledTime - createdAt > 0.25f)
        {
            Puck puck = PuckManager.Instance ? PuckManager.Instance.GetPuck() : null;
            if (puck && Vector3.Distance(playerBody.transform.position, puck.transform.position) < 2f)
            {
                Destroy(gameObject);
            }
        }
    }

    private void ResolvePlayer()
    {
        if (!PlayerManager.Instance) return;
        player = PlayerManager.Instance.GetPlayerByClientId(ownerClientId);
        playerBody = StickCompatibility.GetPlayerBody(player);
    }

    private Vector3 GetAnchorPosition()
    {
        if ((powerupName == PowerupNames.Magnet || powerupName == PowerupNames.Glue) && playerBody.Stick)
        {
            return playerBody.Stick.BladeHandlePosition;
        }

        if (powerupName == PowerupNames.Grapple)
        {
            return playerBody.transform.position;
        }

        if (powerupName == PowerupNames.Jetpack)
        {
            return playerBody.transform.position - playerBody.transform.up * 0.45f;
        }

        return playerBody.transform.position + playerBody.transform.up * 0.65f;
    }

    private void ConfigureEffect()
    {
        switch (powerupName)
        {
            case PowerupNames.Magnet:
                CreateParticles(24f, 0.65f, 0.18f, 1.2f, ParticleSystemShapeType.Sphere, 1.25f);
                CreateTether(0.075f);
                CreateGlow(2.2f);
                break;
            case PowerupNames.Rage:
                CreateParticles(58f, 1.6f, 0.13f, 1.15f, ParticleSystemShapeType.Sphere, 1.15f);
                CreateGlow(3.4f);
                spinSpeed = 150f;
                break;
            case PowerupNames.Grapple:
                CreateParticles(18f, 2.5f, 0.09f, 0.65f, ParticleSystemShapeType.Cone, 0.55f);
                CreateTether(0.11f);
                break;
            case PowerupNames.Glue:
                CreateParticles(16f, 0.15f, 0.22f, 1.8f, ParticleSystemShapeType.Sphere, 0.55f, -0.18f);
                break;
            case PowerupNames.Kick:
                CreateParticles(90f, 5.5f, 0.14f, 0.55f, ParticleSystemShapeType.Hemisphere, 0.65f);
                CreateRing(1.0f, false, 0.09f);
                CreateGlow(2.5f);
                fallbackDestroyAt = createdAt + 1.25f;
                break;
            case PowerupNames.Tornado:
                CreateParticles(72f, 3.1f, 0.12f, 1.15f, ParticleSystemShapeType.Cone, 2.5f, -0.2f);
                CreateRing(2.7f, false, 0.07f);
                spinSpeed = 320f;
                break;
            case PowerupNames.Jetpack:
                CreateParticles(68f, 5.2f, 0.13f, 0.55f, ParticleSystemShapeType.Cone, 0.45f, 0.35f);
                CreateTrail(0.24f, 0.45f);
                CreateGlow(2f);
                break;
            case PowerupNames.LowGrav:
                CreateParticles(26f, 1.25f, 0.14f, 2.1f, ParticleSystemShapeType.Sphere, 1.35f, -0.42f);
                CreateRing(1.25f, false, 0.055f);
                spinSpeed = 45f;
                break;
            case PowerupNames.Turbo:
                CreateParticles(46f, 4.2f, 0.09f, 0.55f, ParticleSystemShapeType.Cone, 0.8f);
                CreateTrail(0.32f, 0.7f);
                CreateGlow(1.8f);
                break;
            case PowerupNames.Backflip:
                CreateParticles(32f, 2.4f, 0.1f, 0.65f, ParticleSystemShapeType.Circle, 1f);
                CreateRing(1.3f, true, 0.085f);
                CreateTrail(0.18f, 0.9f);
                spinSpeed = 260f;
                break;
            case PowerupNames.Slowmo:
                CreateParticles(12f, 0.12f, 0.18f, 2.4f, ParticleSystemShapeType.Sphere, 1.45f, -0.08f);
                CreateRing(1.45f, true, 0.07f);
                CreateGlow(2.8f);
                spinSpeed = 22f;
                break;
        }
    }

    private void CreateParticles(float emissionRate, float speed, float size, float lifetime, ParticleSystemShapeType shapeType, float radius, float gravity = 0f)
    {
        GameObject particleObject = new GameObject("Particles");
        particleObject.transform.SetParent(transform, false);
        particles = particleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.35f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.maxParticles = 320;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = emissionRate;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = shapeType;
        shape.radius = radius;
        if (shapeType == ParticleSystemShapeType.Cone)
        {
            shape.angle = powerupName == PowerupNames.Tornado ? 28f : 15f;
            shape.length = powerupName == PowerupNames.Tornado ? 2.5f : 0.6f;
        }

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.Lerp(color, Color.white, 0.4f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = GetParticleMaterial();

        particles.Play();
    }

    private void CreateTether(float width)
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width * 0.45f;
        line.startColor = color;
        line.endColor = new Color(color.r, color.g, color.b, 0.2f);
        line.numCapVertices = 4;
        line.material = GetLineMaterial();
    }

    private void CreateRing(float radius, bool vertical, float width)
    {
        ringRadius = radius;
        verticalRing = vertical;
        line = gameObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 48;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.numCornerVertices = 3;
        line.material = GetLineMaterial();
        SetRingPositions(radius);
    }

    private void CreateTrail(float width, float lifetime)
    {
        trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = lifetime;
        trail.startWidth = width;
        trail.endWidth = 0f;
        trail.startColor = color;
        trail.endColor = new Color(color.r, color.g, color.b, 0f);
        trail.numCapVertices = 3;
        trail.material = GetLineMaterial();
        trail.emitting = true;
    }

    private void CreateGlow(float range)
    {
        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = range;
        glow.intensity = 1.4f;
        glow.shadows = LightShadows.None;
    }

    private void UpdateLine()
    {
        if (!line || ringRadius > 0f) return;

        Puck puck = PuckManager.Instance ? PuckManager.Instance.GetPuck() : null;
        if (!puck)
        {
            line.enabled = false;
            return;
        }

        Vector3 start = GetAnchorPosition();
        Vector3 end = puck.transform.position;
        float distance = Vector3.Distance(start, end);
        if (powerupName == PowerupNames.Magnet && distance > 3.5f)
        {
            line.enabled = false;
            return;
        }

        line.enabled = true;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void UpdateRing()
    {
        if (!line || ringRadius <= 0f) return;

        float age = Time.unscaledTime - createdAt;
        float pulse = 1f + Mathf.Sin(age * (powerupName == PowerupNames.Slowmo ? 2f : 6f)) * 0.12f;
        if (powerupName == PowerupNames.Kick) pulse = 1f + age * 2.4f;
        SetRingPositions(ringRadius * pulse);
    }

    private void SetRingPositions(float radius)
    {
        if (!line) return;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i / (float)line.positionCount * Mathf.PI * 2f;
            Vector3 position = verticalRing
                ? new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f)
                : new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            line.SetPosition(i, position);
        }
    }

    private static Material GetParticleMaterial()
    {
        if (sharedParticleMaterial) return sharedParticleMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");
        sharedParticleMaterial = new Material(shader) { name = "Powerups Particle Material" };
        return sharedParticleMaterial;
    }

    private static Material GetLineMaterial()
    {
        if (sharedLineMaterial) return sharedLineMaterial;

        Shader shader = Shader.Find("Sprites/Default") ??
                        Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Unlit/Color");
        sharedLineMaterial = new Material(shader) { name = "Powerups Line Material" };
        return sharedLineMaterial;
    }
}
