(*
    A. Baygeldin (c) 2014
    Works with VK audio via API.
*)

module VkAudio

open System.Net
open System.Xml
open System.Text
open System.Windows

type Song = { Number : int; ID : string; Artist : string; Title : string; Url : string; LyricsID : string }

type VkAudio() =
    let authSetted = new Event<_>()
    let mutable accessToken = null
    let mutable userID = null

    member private this.GetSongsUrl (list:string list option) =
        "https://api.vk.com/method/audio.get.xml?access_token=" 
        + this.AccessToken + "&owner_id=" + this.UserID
        + if list.IsSome then "&audio_ids=" + Seq.fold (fun acc id -> id + "," + acc) null list.Value else null
        + "&count=6000"

    member private this.GetLyricsUrl id =
        "https://api.vk.com/method/audio.getLyrics.xml?access_token="
        + this.AccessToken + "&lyrics_id=" + id

    member this.GetNameUrl = 
        "https://api.vk.com/method/users.get.xml?user_ids=" + this.UserID

    member private this.parseName (xmlDoc:XmlDocument) = 
        xmlDoc.SelectSingleNode("/response/user/first_name").InnerText
        + " " + xmlDoc.SelectSingleNode("/response/user/last_name").InnerText

    member private this.parseSong (acc:Song list) elem : Song list = 
        let elem = elem :> obj :?> XmlNode
        let lyrics_id = 
            let node = elem.SelectSingleNode("lyrics_id")
            if node = null then null else node.InnerText
        let replace (s:string) = 
            s.Replace("&amp;", "&").Replace("&lt;", "<")
                .Replace("&gt;", ">").Replace("&quot;", "\"")
                .Replace("&apos;", "'")
        let song = 
            { Number = acc.Length + 1;
            ID = elem.SelectSingleNode("aid").InnerText;
            Artist = elem.SelectSingleNode("artist").InnerText |> replace;
            Title = elem.SelectSingleNode("title").InnerText |> replace;
            Url = elem.SelectSingleNode("url").InnerText;
            LyricsID = lyrics_id }
        song::acc
    
    member private this.toUTF8 (xmlDoc:XmlDocument) (s:string) = 
        s |> Encoding.Default.GetBytes |> Encoding.UTF8.GetString |> xmlDoc.LoadXml

    member private this.parseAll (xmlDoc:XmlDocument) = 
        xmlDoc.SelectNodes("/response/audio") |> Seq.cast<XmlNode> 
        |> Seq.toList |> List.rev |> List.toSeq |> Seq.fold (this.parseSong) []

    ///Occurs when authorization has setted
    [<CLIEvent>]
    member this.AuthSetted = authSetted.Publish

    ///Gets or sets the App ID
    member val AppID = null with get, set

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
    member this.GetLink () =
        "https://oauth.vk.com/authorize?client_id="   
        + this.AppID + "&scope=audio&redirect_uri="
        + "https://oauth.vk.com/blank.html"
        + "&display=page&v=5.24&response_type=token"
    
    ///Gets owners full name
    member this.GetName =
        let webClient = new WebClient()
        let xmlDoc = new XmlDocument()
        this.GetNameUrl |> webClient.DownloadString |> this.toUTF8 xmlDoc
        this.parseName xmlDoc

    ///Gets owners full name asynchronously
    member this.GetNameAsync =
        let webClient = new WebClient()
        let xmlDoc = new XmlDocument()
        async {
            let! data = new System.Uri(this.GetNameUrl) |> webClient.AsyncDownloadString
            data |> this.toUTF8 xmlDoc
            return this.parseName xmlDoc
        }
    
    ///Gets lyrics for a song
    member this.GetLyrics id = 
        let webClient = new WebClient()
        let xmlDoc = new XmlDocument()
        this.GetLyricsUrl id |> webClient.DownloadString |> this.toUTF8 xmlDoc
        try
            xmlDoc.SelectSingleNode("/response/lyrics/text").InnerText
        with
            | _ -> failwith "Too many requeusts per second."

    ///Gets lyrics for a song asynchronously
    member this.GetLyricsAsync id = 
        let webClient = new WebClient()
        let xmlDoc = new XmlDocument()
        async {
            let! data = new System.Uri(this.GetLyricsUrl id) |> webClient.AsyncDownloadString
            data |> this.toUTF8 xmlDoc
            try
                return xmlDoc.SelectSingleNode("/response/lyrics/text").InnerText
            with
                | _ -> return failwith "Too many requeusts per second."
        }

    ///Gets a list of audio files of a user (ids are optional)
    member this.GetSongs (?list:string list) = 
        let webClient = new WebClient()
        let xmlDoc = new XmlDocument()
        this.GetSongsUrl list |> webClient.DownloadString
        |> this.toUTF8 xmlDoc
        this.parseAll xmlDoc

    ///Gets a list of audio files of a user asynchronously (ids are optional)
    member this.GetSongsAsync (?list:string list) = 
        let webClient = new WebClient()
        let xmlDoc = new XmlDocument()
        async {
            let! data = new System.Uri(this.GetSongsUrl list) |> webClient.AsyncDownloadString
            this.toUTF8 xmlDoc data
            return this.parseAll xmlDoc
        }