using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GetUsersByGroupName.Models;
internal class ResponseObject
{
    public string? status { get; set; }

    public string? message { get; set; }

    public Payload? payload { get; set; }
}
