using System;
using tei_penService_ui.Models;
using TeiPenServiceConnectionManager.Models;

namespace tei_penService_ui.Helpers
{
    public static class UserMemoryHelper
    {
       public static  UserMemoryEntry User = new UserMemoryEntry()
       {
        Id = "93c152a6-7998-4b19-afdf-d34fce6de589",
        Email = "w.chrosnik1@gmail.com",
        DisplayName = "Willy Chrosnik",
        Password = "1234",
        LinkedPenMacAddress = null,
        CreatedAt = DateTime.Now
       };

       public static PenMemoryEntry Pen = new PenMemoryEntry()
       {
      MacAddress = "9C:7B:D2:1A:19:E6",
      DeviceId = "BluetoothLE#BluetoothLE00:e0:4c:23:99:87-c9:67:33:a9:8c:c9",
      PenName = "Willy Chrosnik",
      DisplayName = "NWP-F45",
      Protocol = -1,
      FirstConnectedAt = DateTime.Now,
      LastConnectedAt = DateTime.Now,
      Password = null
       };
    }
}