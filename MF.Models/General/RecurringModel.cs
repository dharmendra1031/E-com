using System;
using System.Collections.Generic;
using System.Text;

namespace MF.Models
{
    public class RecurringModel
    {
        public string RecurringType { get; set; }
        public int? IntervalDays { get; set; }
        public int? Iteration { get; set; }
    }
}
