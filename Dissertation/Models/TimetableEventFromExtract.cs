using Dissertation.Models.Database;
using System;
using System.Collections.Generic;

namespace Dissertation.Models
{
    public class TimetableEventFromExtract
    {
        public int StudentId { get; set; }

        public DateTime EventDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public string LocationId { get; set; }

        public TimetableEvent MapToTimetableEvent(HashSet<string> possibleRooms)
        {
            TimetableEvent output = new TimetableEvent
            {
                EventDate = EventDate,
                StartTime = StartTime,
                EndTime = EndTime,
                StudentId = StudentId
            };

            // Ignore any extra rooms, use the first one.
            string[] rooms = LocationId.Split(",");

            int indexOfFirstDigit = rooms[0].IndexOfAny(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });

            if (indexOfFirstDigit < 0)
            {
                return default;
            }
            int firstDigitLength = indexOfFirstDigit + 1;
            string buildingAndFloor = rooms[0].Substring(0, firstDigitLength);
            string remainder = rooms[0].Substring(firstDigitLength, rooms[0].Length - firstDigitLength);

            int indexOfDash = remainder.IndexOf('-');
            if (indexOfDash >= 0)
            {
                // Ignore everything after a dash if there is one. Dashes are used to add extra info to the room code but do not change the room.
                remainder = remainder.Substring(0, indexOfDash);
            }

            string roomId = $"r_{buildingAndFloor}_{remainder}".ToLower();

            if (!possibleRooms.Contains(roomId))
            {
                return default;
            }

            output.LocationNodeId = roomId;
            return output;
        }
    }
}