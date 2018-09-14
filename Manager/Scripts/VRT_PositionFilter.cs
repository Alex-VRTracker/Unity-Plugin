﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CircularBuffer;

namespace VRTracker.Utils
{
    public class VRT_PositionFilter
    {

        private float positionLatency = 0.065f; // Delay between position and acceleration
        private float predictionDelay = 0.02f;

        private float maxPredictionDelaySinceLastMeasurement = 0.5f;
        private float maxDelaySinceLastMeasurement = 0.1f;
        private float discardSpeed = 2.0f; // Max speed before detecting a jump
        private float discardDistance = 0.15f;
        private float speedCalculationDelay = 0.14f;
        private float accelerationOnlyTrackingDelay = 0.30f; // delay during which we keep tracking with acceleration measurement while NOT receiving position udpates
        private float maxSpeedViabilityDelay = 0.5f;
        private float maxAccelerationViabilityDelay = 0.1f; // maximum delay without accelaration measurement updates during which we can use the last acceleration

        private List<PositionOffset> offsets = new List<PositionOffset>();

        private OneEuroFilter<Vector3> oneEuro = new OneEuroFilter<Vector3>(90, 5.0f, 0.2f, 1.0f);

        private Vector3 lastCalculatedPosition = Vector3.zero;
        private double lastCalculatedPositionTimestamp = 0.0d;

        CircularBuffer<TrackingData> trackingDataBuffer;

        public VRT_PositionFilter()
        {
            Init();

        }

        public void Init()
        {
            trackingDataBuffer = new CircularBuffer<TrackingData>(100);
        }


        // Use this for initialization
        void Start()
        {
            Init();
            Test();
            PrintBuffer();
        }

        public Vector3 GetPosition(double timestamp)
        {
            lock (trackingDataBuffer)
            {

            int lastPositionIndex = GetLastPositionIndex();
            if (lastPositionIndex == -1)
            {
                //  Debug.LogWarning("Couldn't find any last position");
                return lastCalculatedPosition;
            }

            float delaySinceLastGetPosition = (float)(timestamp - lastCalculatedPositionTimestamp);
            float delaySinceLastPositionMeasurement = lastPositionIndex == -1 ? maxSpeedViabilityDelay : (float)(timestamp - trackingDataBuffer[lastPositionIndex].timestamp);
            float delaySinceLastMeasurement = (float)(timestamp - (trackingDataBuffer.Size > 0 ? trackingDataBuffer[0].timestamp : 0.0d));

            if (delaySinceLastMeasurement > maxPredictionDelaySinceLastMeasurement || trackingDataBuffer.Size < 1)
                return lastCalculatedPosition;


            int lastAccelerationIndex = GetLastAccelerationIndex();
            Vector3 lastAcceleration = lastAccelerationIndex == -1 ? Vector3.zero : ((TrackingDataIMU)trackingDataBuffer[lastAccelerationIndex]).acceleration;
            float delaySinceLastAccelerationMeasurement = lastAccelerationIndex == -1 ? maxAccelerationViabilityDelay : (float)(timestamp - trackingDataBuffer[lastAccelerationIndex].timestamp);

            List<PositionOffset> deleteList = new List<PositionOffset>();
            Vector3 currentOffset = Vector3.zero;
            foreach (PositionOffset off in offsets)
            {
                currentOffset += off.GetOffset(timestamp);
                if (off.CorrectionOver(timestamp))
                    deleteList.Add(off);

            }

            foreach (PositionOffset off in deleteList)
                offsets.Remove(off);

            // ACC
            if (trackingDataBuffer[0].GetType() == typeof(TrackingDataIMU))
            {
                // Position calculation with Slerp in case we have been predicting with ACC for too long
                Vector3 newOffset = Vector3.Slerp(trackingDataBuffer[0].speed, Vector3.zero, delaySinceLastPositionMeasurement / maxSpeedViabilityDelay) * delaySinceLastGetPosition + 0.5f * Vector3.Slerp(lastAcceleration, Vector3.zero, delaySinceLastAccelerationMeasurement / maxAccelerationViabilityDelay) * delaySinceLastGetPosition * delaySinceLastGetPosition;
                lastCalculatedPosition = Vector3.Slerp(lastCalculatedPosition + newOffset, lastCalculatedPosition, delaySinceLastPositionMeasurement / accelerationOnlyTrackingDelay);
                lastCalculatedPositionTimestamp = timestamp;

            }

            // POS
            else
            {
                // Position calculation with Slerp in case we have been predicting with ACC for too long
                Vector3 newOffset = Vector3.Slerp(trackingDataBuffer[0].speed, Vector3.zero, delaySinceLastPositionMeasurement / maxSpeedViabilityDelay) * delaySinceLastGetPosition + 0.5f * Vector3.Slerp(lastAcceleration, Vector3.zero, delaySinceLastAccelerationMeasurement / maxAccelerationViabilityDelay) * delaySinceLastGetPosition * delaySinceLastGetPosition;
                lastCalculatedPosition = Vector3.Slerp(lastCalculatedPosition + newOffset, lastCalculatedPosition, delaySinceLastPositionMeasurement / accelerationOnlyTrackingDelay);
                lastCalculatedPositionTimestamp = timestamp;
            }
            // return lastCalculatedPosition + currentOffset;
            return oneEuro.Filter(lastCalculatedPosition + currentOffset, (float)timestamp);
        }
        }

        /// <summary>
        /// Adds the position measurement.
        /// </summary>
        /// <param name="timestamp">Timestamp.</param>
        /// <param name="position">Position.</param>
        public void AddPositionMeasurement(double timestamp, Vector3 position)
        {
            lock (trackingDataBuffer)
            {
            TrackingDataPosition trackingDataPosition = new TrackingDataPosition(timestamp - positionLatency, position);
            int index = InsertByTimestamp(trackingDataPosition);

            //   Debug.Log("POS MEASUREMENT AT  " + (timestamp-positionLatency).ToString("0.000") + " POS: " + position.ToString("0.000"));

            // Check there is no position with a more recent timestamp, either a network error or impossible state
            for (int i = index - 1; i >= 0; i--)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataPosition))
                {
                    Debug.LogError("The position received is older than other position");
                    //TODO Remove from buffer ?
                    return;
                }
            }

            if (trackingDataBuffer.Size < 2)
                return;

            // Check time since last measurement (of any type : position or acc)
            double delaySinceLastUpdate = trackingDataPosition.timestamp - trackingDataBuffer[index + 1].timestamp;
            // if (delaySinceLastUpdate > maxDelaySinceLastMeasurement)
            //   Debug.LogWarning("Too long delay since last update : " + delaySinceLastUpdate.ToString());

            bool predictSpeedFromAcc = false; // Boolean to che kif we should calculate the speed from the last acceleration (in case of jump, no position etc)

            // Try to find position jumps
            TrackingDataPosition previousPosition = GetPreviousPositionData(index);
            if (previousPosition == null)
                return;

            float speedMagnitudeLastPositions = ((trackingDataPosition.position - previousPosition.position) / (float)(trackingDataPosition.timestamp - previousPosition.timestamp)).magnitude;
            if (speedMagnitudeLastPositions > discardSpeed && (trackingDataPosition.position - previousPosition.position).magnitude > discardDistance)
            {
                ((TrackingDataPosition)trackingDataBuffer[index]).jump = true;
                predictSpeedFromAcc = true;
                //    Debug.LogWarning("Jump detected of speed " + speedMagnitudeLastPositions.ToString() + "  at TS " + trackingDataPosition.timestamp.ToString());
            }


            if (!predictSpeedFromAcc)
            {
                List<TrackingDataPosition> lastPositions = GetPreviousPositionData(index, speedCalculationDelay);
                // There is not last position
                if (lastPositions.Count <= 1)
                {
                    predictSpeedFromAcc = true;
                 //   Debug.LogError("Could not find any previous position " + index.ToString() + "  - " + trackingDataPosition.timestamp.ToString() + "  count " + lastPositions.Count.ToString() + "  TS: " + timestamp.ToString("0.000"));
                }
                else
                {
                    // Check for jump in last positions
                    foreach (TrackingDataPosition pos in lastPositions)
                        if (pos.jump)
                        {
                            predictSpeedFromAcc = true;
                            break;
                        }

                    // Calculate speed from last positions
                    if (!predictSpeedFromAcc)
                    {
                        Vector3 speed = (lastPositions[0].position - lastPositions[lastPositions.Count - 1].position) / (float)(lastPositions[0].timestamp - lastPositions[lastPositions.Count - 1].timestamp);
                        double speedtimestamp = (lastPositions[0].timestamp + lastPositions[lastPositions.Count - 1].timestamp) / 2.0d;
                        //  Debug.Log("     Calculating speed form pos : " + speed.magnitude.ToString("0.000"));
                        // Propagate with acceleration until this time
                        List<TrackingDataIMU> accelerations = GetPreviousIMUDataUntilTimestamp(index, speedtimestamp);
                        if (accelerations.Count > 0)
                        {
                            for (int i = accelerations.Count - 1; i >= 0; i--)
                            {
                                speed += accelerations[i].acceleration * (float)(accelerations[i].timestamp - speedtimestamp);
                                speedtimestamp = accelerations[i].timestamp;
                            }
                            // Could be improved here (like by checking if we have another acc jsut after current index)
                            speed += accelerations[0].acceleration * (float)(trackingDataBuffer[index].timestamp - accelerations[0].timestamp);
                            //  Debug.Log("     Predicting speed from acc to ts : " + speed.magnitude.ToString("0.000"));

                        }
                        ((TrackingDataPosition)trackingDataBuffer[index]).speed = speed;
                    }
                }
            }

            // Calculate speed from previous acceleration
            if (predictSpeedFromAcc)
            {

                // The previous data is an acceleration
                if (trackingDataBuffer[index + 1].GetType() == typeof(TrackingDataIMU))
                {
                    //TODO: handle offset correction ?
                    TrackingDataIMU previousAcceleration = ((TrackingDataIMU)trackingDataBuffer[index + 1]);

                    // Predict position and speed at this update
                    trackingDataBuffer[index].speed = previousAcceleration.speed + previousAcceleration.acceleration * (float)delaySinceLastUpdate;
                    //  Debug.Log("     Predicting speed from acc : " + trackingDataBuffer[index].speed.magnitude.ToString("0.000"));
                }
                // The previous data is a position
                else if (trackingDataBuffer[index + 1].GetType() == typeof(TrackingDataPosition))
                {
                    //TODO: handle offset correction
                    // No prediction : use speed of last position
                    ((TrackingDataPosition)trackingDataBuffer[index + 1]).speed = previousPosition.speed;
                    //    Debug.Log("     Using previous speed : " + previousPosition.speed.magnitude.ToString("0.000"));
                }
            }

            // Correct following positions and speeds 
            // Keep latest position before propagation to calculate the offset
            Vector3 latestPosition = trackingDataBuffer[0].position;
            for (int i = index - 1; i >= 0; i--)
            {
                // At this point we can only have IMU data up to the end of the vector
                double deltaTime = trackingDataBuffer[i].timestamp - trackingDataBuffer[i + 1].timestamp;

                trackingDataBuffer[i].speed = trackingDataBuffer[i + 1].speed + ((TrackingDataIMU)trackingDataBuffer[i]).acceleration * (float)deltaTime;
                trackingDataBuffer[i].position = trackingDataBuffer[i + 1].position + trackingDataBuffer[i + 1].speed * (float)deltaTime + 0.5f * ((TrackingDataIMU)trackingDataBuffer[i]).acceleration * (float)deltaTime * (float)deltaTime;
            }

            Vector3 positionOffsetAfterPropagation = trackingDataBuffer[0].position - latestPosition;
            if (positionOffsetAfterPropagation.magnitude > 0.04f)
            {
                offsets.Add(new PositionOffset(trackingDataPosition.timestamp, previousPosition.position - trackingDataPosition.position));
                //      Debug.LogWarning("     Offset : " + positionOffsetAfterPropagation.magnitude.ToString("0.000") + "   at " + trackingDataBuffer[0].timestamp.ToString("0.000"));
            }
            lastCalculatedPosition = trackingDataBuffer[0].position;
            lastCalculatedPositionTimestamp = trackingDataBuffer[0].timestamp;
            if (lastCalculatedPosition.magnitude > 5)
                Debug.LogError("TS " + lastCalculatedPositionTimestamp.ToString("0.000") + "  MAG: " + lastCalculatedPosition.magnitude.ToString());
        }
        }

        /// <summary>
        /// Adds the acceleration measurement.
        /// </summary>
        /// <param name="timestamp">Timestamp.</param>
        /// <param name="acceleration">Acceleration.</param>
        public void AddAccelerationMeasurement(double timestamp, Vector3 acceleration)
        {
            lock (trackingDataBuffer)
            {
                TrackingDataIMU trackingDataIMU = new TrackingDataIMU(timestamp, acceleration);
                int index = InsertByTimestamp(trackingDataIMU);
                //  Debug.Log("ACC MEASUREMENT AT  " + timestamp.ToString("0.000") + " MAG: " + acceleration.magnitude.ToString("0.000"));

                if (index != 0)
                    Debug.LogError("Acceleration was not insered at last position  " + timestamp.ToString("0.000"));

                if (trackingDataBuffer.Size < 2)
                    return;

                double delaySinceLastUpdate = trackingDataIMU.timestamp - trackingDataBuffer[index + 1].timestamp;
                if (delaySinceLastUpdate > maxDelaySinceLastMeasurement)
                {
                    Debug.LogWarning("Too long delay since last update : " + delaySinceLastUpdate.ToString() + "  " + timestamp.ToString("0.000"));
                    return;
                }

                double lastpositionts = GetLastPositionTimestamp();
                //        Debug.Log("Last pos ts " + lastpositionts.ToString("0.000"));
                if (lastpositionts < 0 || timestamp - lastpositionts > accelerationOnlyTrackingDelay)
                {
                    //  Debug.LogError("No previous position, or too long ago");
                    return;
                }

                // Calculate its speed and position using the previous info

                // The previous data is an acceleration
                if (trackingDataBuffer[index + 1].GetType() == typeof(TrackingDataIMU))
                {
                    //TODO: handle offset correction ?
                    TrackingDataIMU previousAcceleration = ((TrackingDataIMU)trackingDataBuffer[index + 1]);

                    if (delaySinceLastUpdate < 0.002f)
                    {
                     //   Debug.LogError("Too short delay since previous acceleration (Networking issue) " + timestamp.ToString("0.000"));
                        return;
                    }
                    // Predict position and speed at this update

                    // Clamping to avoid predicting for too long
                    Vector3 newSpeed = (timestamp - lastpositionts) < 0.11f ? previousAcceleration.speed + ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate : Vector3.Slerp(previousAcceleration.speed + ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate, Vector3.zero, (float)(timestamp - lastpositionts) / accelerationOnlyTrackingDelay);
                    trackingDataBuffer[index].speed = newSpeed;
                    Vector3 newPositionOffset = (timestamp - lastpositionts) < 0.11f ? previousAcceleration.speed * (float)delaySinceLastUpdate + 0.5f * ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate * (float)delaySinceLastUpdate : Vector3.Slerp(previousAcceleration.speed * (float)delaySinceLastUpdate + 0.5f * ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate * (float)delaySinceLastUpdate, Vector3.zero, (float)(timestamp - lastpositionts) / accelerationOnlyTrackingDelay);
                    trackingDataBuffer[index].position = previousAcceleration.position + newPositionOffset;
                    //    Debug.Log("     Offset from prev acceleration : " + (trackingDataBuffer[index].position - previousAcceleration.position).magnitude.ToString("0.000"));
                }
                // The previous data is a position
                else if (trackingDataBuffer[index + 1].GetType() == typeof(TrackingDataPosition))
                {
                    //TODO: handle offset correction
                    TrackingDataPosition previousPosition = ((TrackingDataPosition)trackingDataBuffer[index + 1]);
                    // Predict position and speed at this update
                    Vector3 newSpeed = delaySinceLastUpdate < 0.03f ? previousPosition.speed + ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate : Vector3.Slerp(previousPosition.speed + ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate, Vector3.zero, (float)(delaySinceLastUpdate) / accelerationOnlyTrackingDelay);
                    trackingDataBuffer[index].speed = newSpeed;
                    Vector3 newPositionOffset = delaySinceLastUpdate < 0.03f ? previousPosition.speed * (float)delaySinceLastUpdate + 0.5f * ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate * (float)delaySinceLastUpdate : Vector3.Slerp(previousPosition.speed * (float)delaySinceLastUpdate + 0.5f * ((TrackingDataIMU)trackingDataBuffer[index]).acceleration * (float)delaySinceLastUpdate * (float)delaySinceLastUpdate, Vector3.zero, (float)(timestamp - lastpositionts) / accelerationOnlyTrackingDelay);
                    trackingDataBuffer[index].position = previousPosition.position + newPositionOffset;
                    //   Debug.Log("     Offset from prev position : " + (trackingDataBuffer[index].position-previousPosition.position).magnitude.ToString("0.000"));
                }

                lastCalculatedPosition = trackingDataBuffer[0].position;
                lastCalculatedPositionTimestamp = trackingDataBuffer[0].timestamp;
               // if (lastCalculatedPosition.magnitude > 5)
                 //   Debug.LogError("TS " + lastCalculatedPositionTimestamp.ToString("0.000") + "  MAG: " + lastCalculatedPosition.magnitude.ToString());

            }
        }

        /// <summary>
        /// Gets the previous IMU Data before the index.
        /// </summary>
        /// <returns>The previous IMUD ata.</returns>
        /// <param name="index">Index.</param>
        private TrackingDataIMU GetPreviousIMUData(int index)
        {

            for (int i = index + 1; i < trackingDataBuffer.Size; i++)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataIMU))
                    return (TrackingDataIMU)trackingDataBuffer[i];
            }

            Debug.LogError("Could not find previous IMU Data");
            return null;
        }

        /// <summary>
        /// Returns all the previous acceleration data between index (excluded) and previous timestamp
        /// Note the timestamp at index is supposed to be greater than timestamp
        /// </summary>
        /// <returns>The previous IMUD ata until timestamp.</returns>
        /// <param name="index">Index.</param>
        /// <param name="timestamp">Timestamp.</param>
        private List<TrackingDataIMU> GetPreviousIMUDataUntilTimestamp(int index, double timestamp)
        {
            List<TrackingDataIMU> accelerations = new List<TrackingDataIMU>();

            for (int i = index + 1; i < trackingDataBuffer.Size && trackingDataBuffer[i].timestamp > timestamp; i++)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataIMU))
                    accelerations.Add((TrackingDataIMU)trackingDataBuffer[i]);
            }

            return accelerations;

        }

        /// <summary>
        /// Gets the previous position data before the index.
        /// </summary>
        /// <returns>The previous position data.</returns>
        /// <param name="index">Index.</param>
        private TrackingDataPosition GetPreviousPositionData(int index)
        {

            for (int i = index + 1; i < trackingDataBuffer.Size; i++)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataPosition))
                    return (TrackingDataPosition)trackingDataBuffer[i];
            }

            Debug.LogError("Could not find previous Position Data");
            return null;
        }

        /// <summary>
        /// Gets the previous positions starting at index (including) and until 
        /// a "delay" since the index.
        /// </summary>
        /// <returns>The previous position data.</returns>
        /// <param name="index">Index.</param>
        /// <param name="delay">Delay.</param>
        private List<TrackingDataPosition> GetPreviousPositionData(int index, double delay)
        {
            List<TrackingDataPosition> positions = new List<TrackingDataPosition>();
            double startTimestamp = trackingDataBuffer[index].timestamp;
            bool positionFound = false;
            for (int i = index; i < trackingDataBuffer.Size && ((startTimestamp - trackingDataBuffer[i].timestamp) < delay); i++) // || (i < trackingDataBuffer.Size && !positionFound)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataPosition))
                {
                    positions.Add((TrackingDataPosition)trackingDataBuffer[i]);
                    positionFound = true;
                }
            }

            if (!positionFound)
                Debug.LogError("Could not find previous position Data");
            return positions;
        }

        /// <summary>
        /// Inserts in the circulare buffer to keep it ordered
        /// by timestamp.
        /// </summary>
        /// <returns>the index where the data was inserted timestamp.</returns>
        private int InsertByTimestamp(TrackingData data)
        {

            trackingDataBuffer.PushFront(data);

            for (int i = 1; i < trackingDataBuffer.Size; i++)
            {
                if (trackingDataBuffer[i - 1].timestamp < trackingDataBuffer[i].timestamp)
                {
                    // Invert
                    TrackingData tempData = trackingDataBuffer[i];
                    trackingDataBuffer[i] = trackingDataBuffer[i - 1];
                    trackingDataBuffer[i - 1] = tempData;
                }
                else
                    return i - 1;
            }
            return 0;
        }

        private double GetLastPositionTimestamp()
        {
            for (int i = 0; i < trackingDataBuffer.Size; i++)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataPosition))
                    return trackingDataBuffer[i].timestamp;
            }
            return -1.0d;
        }

        private int GetLastPositionIndex()
        {
            for (int i = 0; i < trackingDataBuffer.Size; i++)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataPosition))
                    return i;
            }
            return -1;
        }

        private double GetLastAccelerationTimestamp()
        {
            for (int i = 0; i < trackingDataBuffer.Size; i++)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataIMU))
                    return trackingDataBuffer[i].timestamp;
            }
            return -1;
        }

        private int GetLastAccelerationIndex()
        {
            for (int i = 0; i < trackingDataBuffer.Size; i++)
            {
                if (trackingDataBuffer[i].GetType() == typeof(TrackingDataIMU))
                    return i;
            }
            return -1;
        }


        private void Test()
        {
            AddAccelerationMeasurement(1.0, Vector3.zero);
            AddAccelerationMeasurement(1.012, Vector3.zero);

            AddPositionMeasurement(1.02, Vector3.zero);

            AddAccelerationMeasurement(1.02, Vector3.zero);
            AddPositionMeasurement(1.05, Vector3.zero);



            AddPositionMeasurement(1.08, Vector3.zero);


            List<TrackingDataPosition> poses = GetPreviousPositionData(0, 1.0);
            foreach (TrackingDataPosition pose in poses)
                Debug.Log(pose.timestamp.ToString() + "  " + pose.position.ToString());
            /*
            AddAccelerationMeasurement(1.1, Vector3.zero);
            AddPositionMeasurement(1.11, Vector3.zero);
            AddPositionMeasurement(1.14, Vector3.zero);
    */

        }

        private void PrintBuffer()
        {
            Debug.Log("-----------------------------------------------------------");
            for (int i = 0; i < trackingDataBuffer.Size; i++)
                Debug.Log(trackingDataBuffer[i].timestamp);
        }
    }

    public abstract class TrackingData
    {
        public double timestamp = 0.0d;
        public Vector3 position = Vector3.zero;
        public Vector3 speed = Vector3.zero;

        public Vector3 positionOffset = Vector3.zero; // An offset vector when propagation is applicated
        public double offsetTimeLeft = 0.0d; // duration until the offset should be applied and back to zero
    }

    public class TrackingDataPosition : TrackingData
    {

        public bool jump = false; // If jump is detected at this position

        public TrackingDataPosition(double timestamp_, Vector3 position_)
        {
            timestamp = timestamp_;
            position = position_;
        }

    }

    public class TrackingDataIMU : TrackingData
    {
        public Vector3 acceleration = Vector3.zero;
        public Quaternion orientation;

        public TrackingDataIMU(double timestamp_, Vector3 acceleration_)
        {
            timestamp = timestamp_;
            acceleration = acceleration_;
        }

        public TrackingDataIMU(double timestamp_, Vector3 acceleration_, Quaternion orientation_)
        {
            timestamp = timestamp_;
            acceleration = acceleration_;
            orientation = orientation_;
        }
    }

    public class PositionOffset
    {
        public float correctionDuration = 0.2f;

        public double timestamp = 0;
        public Vector3 offset = Vector3.zero;

        public PositionOffset(double timestamp_, Vector3 offset_)
        {
            this.timestamp = timestamp_;
            this.offset = offset_;
            this.correctionDuration = 1.5f * offset_.magnitude;
        }

        /// <summary>
        /// Returns true if the correction is fully done and we don't need this offset anymore
        /// </summary>
        /// <returns><c>true</c>, if over was correctioned, <c>false</c> otherwise.</returns>
        public bool CorrectionOver(double timestamp_)
        {
            return (timestamp_ - timestamp) > correctionDuration ? true : false;
        }


        public Vector3 GetOffset(double timestamp_)
        {
            return Vector3.Lerp(offset, Vector3.zero, (float)(timestamp_ - timestamp) / correctionDuration);
        }
    }
}