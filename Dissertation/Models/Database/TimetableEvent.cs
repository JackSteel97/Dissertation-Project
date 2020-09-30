using System;

namespace Dissertation.Models.Database
{
    public class TimetableEvent
    {
        public long RowId { get; set; }

        public int StudentId { get; set; }

        public DateTime EventDate { get; set; }

        public TimeSpan StartTime { get; set; }

        public TimeSpan EndTime { get; set; }

        public string LocationNodeId { get; set; }

        public Node LocationNode { get; set; }

        public TimetableEvent()
        {
        }

        public TimetableEvent(int studentId, DateTime date, TimeSpan start, TimeSpan end, string location)
        {
            StudentId = studentId;
            EventDate = date;
            StartTime = start;
            EndTime = end;
            LocationNodeId = location;
        }
    }
}