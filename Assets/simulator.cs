using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;


public class simulator : MonoBehaviour
{
    public float gravity;
    public static int particleNumber = 3600;
    public float initialWidth;
    public float particleRadius = 0.5f;
    public float smoothingRadius = 0.5f;
    public float particleMass = 1.0f;
    public float targetDensity;
    public float pressureMultiplier;
    public float viscosityStrength = 0.1f;
    [Range(2f, 135f)]
    public float boundarySizeX;
    [Range(2f, 135f)]
    public float boundarySizeY;

    public float mouseRadius = 1.0f;
    public float mouseStrength = 3.0f;

    ParticleSystem.Particle[] _shapeBuffer = new ParticleSystem.Particle[particleNumber];

    Vector2[] position = new Vector2[particleNumber];
    Vector2[] predictedPositions = new Vector2[particleNumber];
    Vector2[] velocity = new Vector2[particleNumber];
    float[] densities = new float[particleNumber];

    float maxVelocity = 0.000000001f;
    public float collisionDamping = 1;
    public Gradient colorGradient;

    ParticleSystem particleSystem;
    List<int>[,] gridPoints;
    
    

    private static readonly object lockMe = new object();


    void Start()
    {

        particleSystem = GetComponent<ParticleSystem>();

        for (int i = 0; i < particleNumber; i++)
        {
            velocity[i] = Vector2.zero;
            densities[i] = 0;

            var particle = _shapeBuffer[i];
            int rectangleWidth = (int) Mathf.Sqrt(particleNumber);
            Vector2 pos = new Vector2((i / rectangleWidth) * initialWidth - particleNumber * initialWidth / (2 * rectangleWidth), (i % rectangleWidth) * initialWidth - particleNumber * initialWidth / (2 * rectangleWidth));

            particle.position = pos;
            position[i] = pos;

            particle.startColor = Color.blue;
            particle.startSize = particleRadius;


            // Keep this particle alive forever.
            particle.startLifetime = float.MaxValue;
            particle.remainingLifetime = float.MaxValue;

            _shapeBuffer[i] = particle;
        }
    }


    void Update()
    {
        simulate(Time.deltaTime);
        mouseForce(mouseRadius, mouseStrength);
        DrawCircles();
    }

    void simulate(float deltaTime)
    {
        //SpatialLookup.updateSpatialLookup(position, smoothingRadius);


        /*for (int i = 0; i < particleNumber; i++)
        {
            Debug.Log($"Key: {SpatialLookup.spatialLookup[i].Key}, Value: {SpatialLookup.spatialLookup[i].Value}");
        }

        for (int i = 0; i < particleNumber; i++) {
            Debug.Log($"{SpatialLookup.startIndices[i]}, i = {i}");
        }*/


        maxVelocity = 0.000000001f;

        Parallel.For(0, particleNumber, i =>
        {
            velocity[i] += Vector2.down * gravity * deltaTime;
            predictedPositions[i] = position[i] + velocity[i] * 1 / 120f;
            boundaryCollision(ref predictedPositions[i]);
        });

        gridPoints = gridPositions(predictedPositions);

        Parallel.For(0, particleNumber, i =>
        {
            densities[i] = density(predictedPositions[i]);
        });

        Parallel.For(0, particleNumber, i =>
        {
            Vector2 pressureForce = pressure(i);

            velocity[i] += pressureForce / densities[i] * deltaTime;

            //viscosity(i, deltaTime);

            float temp = velocity[i].magnitude;

            if (temp > maxVelocity)
            {
                maxVelocity = temp;
            }
        });

        Parallel.For(0, particleNumber, i =>
        {
            position[i] += velocity[i] * deltaTime;
            boundaryCollision(ref position[i], ref velocity[i]);
        });


    }
    private void boundaryCollision(ref Vector2 position)
    {

        if (Mathf.Abs(position.x) > boundarySizeX / 2)
        {
            position.x = Mathf.Sign(position.x) * boundarySizeX / 2;
        }
        if (Mathf.Abs(position.y) > boundarySizeY / 2)
        {
            position.y = Mathf.Sign(position.y) * boundarySizeY / 2;
        }

    }

    private void boundaryCollision(ref Vector2 position, ref Vector2 velocity)
    {

        if(Mathf.Abs(position.x) > boundarySizeX / 2)
        {
            velocity.x *= -collisionDamping;
            position.x = Mathf.Sign(position.x) * boundarySizeX / 2;
        }
        if (Mathf.Abs(position.y) > boundarySizeY / 2)
        {
            velocity.y *= -collisionDamping;
            position.y = Mathf.Sign(position.y) * boundarySizeY / 2;
        }

    }
    void DrawCircles()
    {
        Parallel.For(0, particleNumber, i =>
        {
            _shapeBuffer[i].position = position[i];
            _shapeBuffer[i].startSize = particleRadius;

            float t = velocity[i].magnitude / maxVelocity;

            if (t == t)
            {
                _shapeBuffer[i].startColor = colorGradient.Evaluate(t);
            }
        });

        //highLightNearbyParticles();

        GetComponent<ParticleSystem>().SetParticles(_shapeBuffer, particleNumber, 0);
    }

    void highLightNearbyParticles()
    {
        Vector2 mouseCoordinates = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        int gridSquares = 2 * (int)(mouseRadius / smoothingRadius) + 1;
        List<int> closeParticles = pointsWithinRadius(mouseCoordinates, mouseRadius, gridSquares);


        for(int i = 0; i < closeParticles.Count; i++)
        {
            _shapeBuffer[closeParticles[i]].startColor = Color.red;
        }
    }



    void mouseForce(float radius, float strength)
    {
        bool input = false;
        float forceType = 0;

        if(Input.GetKey(KeyCode.Mouse0)) {
            input = true;
            forceType = 1;
        } else if (Input.GetKey(KeyCode.Mouse1))
        {
            input = true;
            forceType = -1;
        }
        float t = Time.deltaTime;

        if (input)
        {
            Vector2 mouseCoordinates = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            int gridSquares = 2 * (int)(mouseRadius / smoothingRadius) + 1;
            List<int> closeParticles = pointsWithinRadius(mouseCoordinates, radius, gridSquares);

            Parallel.For(0, closeParticles.Count, i =>
            {
                Vector2 displacement = (position[closeParticles[i]] - mouseCoordinates);
                float distance = displacement.magnitude;

                velocity[closeParticles[i]] += (1 - distance / radius) * forceType * displacement.normalized * strength * t / densities[closeParticles[i]];

            });
        }
    }

    void viscosity(int index, float deltaTime)
    {
        Vector2 viscosity = Vector2.zero;
        Vector2 pos = predictedPositions[index];

        List<int> closeParticles = pointsWithinRadius(pos, smoothingRadius, 3);

        for (int i = 0; i < closeParticles.Count; i++)
        {
            int other = closeParticles[i];
            Vector2 otherPos = predictedPositions[other];
            float distance = (pos - otherPos).magnitude;
            float scale = viscocistySmoothingFunction(smoothingRadius, distance);
            viscosity += (velocity[other] - velocity[index]) * scale;

        }

        velocity[index] += viscosityStrength * viscosity * deltaTime / densities[index];
    }

    float viscocistySmoothingFunction(float radius, float distance)
    {
        if (distance >= radius) return 0;

        float value = radius * radius - distance * distance;
        return value * value * value;
    }
    float smoothingFunction(float radius, float distance)
    {
        if (distance >= radius) return 0;
        
        float volume = Mathf.PI * Mathf.Pow(radius, 4) / 6;
        return (radius - distance) * (radius - distance) / volume;
    }

    float smoothingFunctionDerivative(float radius, float distance)
    {
        if (distance >= radius) return 0;

        float scale = 12 / (Mathf.PI * Mathf.Pow(radius, 4));

        return (distance - radius) * scale;
    }

    float densityToPressure(float dens)
    {
        float densityError = dens - targetDensity;

        return densityError * pressureMultiplier;
    }
    float density(Vector2 pos)
    {
        float output = 0;

        List<int> closeParticles = pointsWithinRadius(pos, smoothingRadius, 3);

        for(int i = 0; i < closeParticles.Count; i++)
        {
            float distance = (pos - predictedPositions[closeParticles[i]]).magnitude;
            output += particleMass * smoothingFunction(smoothingRadius, distance);
        }

        return output;
    }
    Vector2 pressure(int particleIndex)
    {
        Vector2 output = Vector2.zero;
        Vector2 pos = predictedPositions[particleIndex];

        List<int> closeParticles = pointsWithinRadius(pos, smoothingRadius, 3);

        for (int i = 0; i < closeParticles.Count; i++)
        {
            if(closeParticles[i] != particleIndex)
            {
                float distance = (predictedPositions[closeParticles[i]] - pos).magnitude;
                Vector2 direction;

                if (distance == 0)
                {

                    System.Random random1 = new System.Random();
                    Vector2 randomVector = new Vector2((float)(random1.NextDouble() * 2 - 1), ((float)random1.NextDouble() * 2 - 1)).normalized;
                    direction = randomVector;

                }
                else
                {
                    direction = (predictedPositions[closeParticles[i]] - pos) / distance;
                }
                
                float slope = smoothingFunctionDerivative(smoothingRadius, distance);
                float dens = densities[closeParticles[i]];
                float averagedPressure = (densityToPressure(dens) + densityToPressure(densities[particleIndex])) / 2.0f;
                if(dens != 0)
                {
                    output += averagedPressure * direction * slope * particleMass / dens;
                }
            }
        }

        return output;
    }
    int xLength; 
    int yLength; 

    List<int> pointsWithinRadius(Vector2 pos, float detectionRadius, int gridUnits)
    {
        int[] gridPoint = coordinateToGridPoint(pos);

        int x = gridPoint[0];
        int y = gridPoint[1];


        List<int> output = new List<int>();

        int bounds = gridUnits / 2;

        for(int i = -bounds; i <= bounds; i++)
        {
            for (int j = -bounds; j <= bounds; j++)
            {
                if(x + i > - 1 && x + i < xLength && y + j > -1 && y + j < yLength)
                {
                    if (gridPoints[x + i, y + j] != null)
                    {
                        for(int k = 0; k < gridPoints[x + i, y + j].Count; k++)
                        {
                            if ((predictedPositions[gridPoints[x + i, y + j][k]] - pos).magnitude < detectionRadius)
                            {
                                output.Add(gridPoints[x + i, y + j][k]);
                            }
                        }
                    }

                }
            }
        }

        return output;
    }
    int[] coordinateToGridPoint(Vector2 pos)
    {
        (int x, int y) = ((int)(pos.x / smoothingRadius), (int)(pos.y / smoothingRadius));

        x += (int)(xLength / 2.0f);
        y += (int)(yLength / 2.0f);

        return new int[2] { x, y };
    }

    List<int>[,] gridPositions(Vector2[] positions)
    {
        xLength = (int)(boundarySizeX / smoothingRadius) + 1;
        yLength = (int)(boundarySizeY / smoothingRadius) + 1;


        List<int>[,] output = new List<int>[xLength, yLength];

        Parallel.For(0, positions.Length, i =>
        {
            int[] gridPoint = coordinateToGridPoint(positions[i]);

            if (gridPoint[0] > -1 && gridPoint[0] < xLength && gridPoint[1] > -1 && gridPoint[1] < yLength)
            {
                lock (lockMe)
                {
                    if (output[gridPoint[0], gridPoint[1]] == null)
                    {
                        output[gridPoint[0], gridPoint[1]] = new List<int>();
                    }

                    output[gridPoint[0], gridPoint[1]].Add(i);
                }
                
            }
        });

        return output;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.DrawLine(new Vector3(-boundarySizeX / 2, -boundarySizeY / 2, 0), new Vector3(boundarySizeX / 2, -boundarySizeY / 2, 0));
        Gizmos.DrawLine(new Vector3(-boundarySizeX / 2, boundarySizeY / 2, 0), new Vector3(boundarySizeX / 2, boundarySizeY / 2, 0));
        Gizmos.DrawLine(new Vector3(-boundarySizeX / 2, -boundarySizeY / 2, 0), new Vector3(-boundarySizeX / 2, boundarySizeY / 2, 0));
        Gizmos.DrawLine(new Vector3(boundarySizeX / 2, -boundarySizeY / 2, 0), new Vector3(boundarySizeX / 2, boundarySizeY / 2, 0));
    }
}
