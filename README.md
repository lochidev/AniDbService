### A simple microservice to fetch calendar anime data from https://anidb.net using it's public APIs.
Set the following environment variables before use
1. AniDb_ClientVersion (Both http and udp versions must be the same, ex:- 2)
2. AniDb_HTTPClient (http Client name)
3. AniDb_UDPClient (udp client name)
4. AniDb_Password (anidb password for the udp api as required by the specification)
5. AniDb_User (your username)
6. OPTIONAL, Redis Connection String, defaults to localhost:6379
### Note redis is needed, as the specification requires that every client employ "heavy caching" to avoid bans, there is also an intentional 5 second delay when fetching info from the http api for this reason
