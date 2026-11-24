using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MF.Models
{
    public class CancelTokenRequest
    {
        [Required]
        public string Token { get; set; }
    }
}
