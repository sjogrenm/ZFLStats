using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using Newtonsoft.Json;

namespace ZFLBot;

internal partial class ZFLBot {
    private void SetupMenuHandlers() {
        this.client.ModalSubmitted += this.MenuModalHandler;
        this.client.ButtonExecuted += this.MenuHandler;
        this.client.SelectMenuExecuted += this.MenuHandler;
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

    private async Task MenuHandler(SocketMessageComponent component){
        (string action, string[] ids) = ParseIdFromAction(component.Data.CustomId);
        Debug.WriteLine($"Component: {JsonConvert.SerializeObject(component.Data, new JsonSerializerSettings{ReferenceLoopHandling = ReferenceLoopHandling.Ignore})}");
        if (action.Equals("close")) {
          await DismissMessage(component);
        }
        else {
          AdminMenuHandler(component, action, ids);
          CoachMenuHandler(component, action, ids);
        }
    }
    private async Task MenuModalHandler(SocketModal modal) {
        Debug.WriteLine(JsonConvert.SerializeObject(modal.Data));
        (string action, string[] ids) = ParseIdFromAction(modal.Data.CustomId);
        AdminMenuModalHandler(modal, action, ids);
    }
}
