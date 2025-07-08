using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace ZFLBot;

internal partial class ZFLBot {
    private void SetupMenuHandlers() {
        this.client.ModalSubmitted += this.AdminMenuModalHandler;
        this.client.ButtonExecuted += this.AdminMenuHandler;
        this.client.ButtonExecuted += this.CoachMenuHandler;
        this.client.SelectMenuExecuted += this.AdminMenuHandler;
        this.client.SelectMenuExecuted += this.CoachMenuHandler;
    }

    private (string action, string[] ids) ParseIdFromAction(string CustomId){
        string pattern = @"([^\(\)]+)\(?([^\(\)]+)?\)?";
        Regex r = new Regex(pattern);
        Match m = r.Match(CustomId);
        string action = m.Groups[1].Value;
        string[] ids = m.Groups.Count > 2 ? m.Groups[2].Value.Split(';') : [];
        Debug.WriteLine($"Input: {CustomId}, Action: {action}, Ids: {JsonConvert.SerializeObject(ids)}");
        return (action, ids);
    }
}
