(*
    A. Baygeldin (c) 2014
    Works with VK audio via API.
*)

module VkAudio

open System.Net
open System.Xml
open System.Text
open System.Windows

type Song = { id : string; artist : string; title : string; url : string; lyrics_id : string }

type VkAudio() =
    let authSetted = new Event<_>()
    let webClient = new WebClient()
    let xmlData = new XmlDocument()
    let mutable accessToken = ""
    let mutable userID = ""
    
    ///Occurs when authorization has setted
    [<CLIEvent>]
    member this.AuthSetted = authSetted.Publish

    ///Gets or sets the App ID
    member val AppID = "" with get, set

    ///Gets the access token
    member this.AccessToken
        with get() = accessToken
        and private set(value) = accessToken <- value

    ///Gets the user ID
    member this.UserID
        with get() = userID
        and private set(value) = userID <- value
    
    ///Sets authorization based on URI's fragment identifier
    member this.SetAuth (fragment:string) =
        let parts = fragment.Substring(1).Split('&');
        this.AccessToken <- parts.[0].Split('=').[1]
        this.UserID <- parts.[2].Split('=').[1]
        authSetted.Trigger()
        
    ///Gets the link required for the authorization  
    member this.GetLink =
        "https://oauth.vk.com/authorize?client_id="   
        + this.AppID + "&scope=audio&redirect_uri="
        + "https://oauth.vk.com/blank.html"
        + "&display=page&v=5.24&response_type=token"
    
    ///Gets owners full name
    member this.GetName =
        "https://api.vk.com/method/users.get.xml?user_ids=" + this.UserID
        |> webClient.DownloadString |> Encoding.Default.GetBytes
        |> Encoding.UTF8.GetString |> xmlData.LoadXml
        xmlData.SelectSingleNode("/response/user/first_name").InnerText
        + " " + xmlData.SelectSingleNode("/response/user/last_name").InnerText
    
    ///Gets lyrics for a song
    member this.GetLyrics id = 
        "https://api.vk.com/method/audio.getLyrics.xml?access_token="
        + this.AccessToken + "&lyrics_id=" + id
        |> webClient.DownloadString |> Encoding.Default.GetBytes
        |> Encoding.UTF8.GetString |> xmlData.LoadXml
        xmlData.SelectSingleNode("/response/lyrics/text").InnerText

    ///Gets a list of audio files of a user (ids are optional)
    member this.GetSongs (?list:string list) = 
        let parseSong (acc:Song list) (elem:XmlNode) : Song list = 
            let lyrics_id = 
                let node = elem.SelectSingleNode("lyrics_id")
                if node = null then "" else node.InnerText
            let song = 
                { id = elem.SelectSingleNode("aid").InnerText;
                artist = elem.SelectSingleNode("artist").InnerText;
                title = elem.SelectSingleNode("title").InnerText;
                url = elem.SelectSingleNode("url").InnerText;
                lyrics_id = lyrics_id }
            song::acc
        "https://api.vk.com/method/audio.get.xml?access_token=" 
        + this.AccessToken + "&owner_id=" + this.UserID
        + if list.IsSome then "&audio_ids=" + Seq.fold (fun acc id -> id + "," + acc) "" list.Value else ""
        + "&count=6000" |> webClient.DownloadString
        |> Encoding.Default.GetBytes |> Encoding.UTF8.GetString 
        |> xmlData.LoadXml
        xmlData.SelectNodes("/response/audio") |> Seq.cast<XmlNode>
        |> Seq.fold (parseSong) [] |> List.rev