using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FurnitureApi.DTOs.SMSModule
{
    public class SMSDTO
    {
        public string Name { get; set; }
        public int OrderId { get; set; }
        public string PhoneNumber { get; set; }
    }
}
