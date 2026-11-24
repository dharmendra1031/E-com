using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class CancelRecurringRequest
    {
        [Required]
        public string RecurringId { get; set; }
    }
}
