// Runtime configuration, loaded before the app bundle and NOT built into it -
// edit this file directly on the server after publishing (no rebuild needed).
window.__APP_CONFIG__ = {
  // null (default): call the API on this same page's hostname/IP, port 5000.
  //   Works with no edits for the common IIS setup - this frontend and the
  //   API as two separate sites/ports on the same server - and keeps working
  //   if the server's IP or site name changes later.
  // '' (empty string): call the API at this exact same origin (no port
  //   added). Use this only if the API is merged into the same site/port as
  //   this app (it serves both the SPA and /api/*, see backend Program.cs).
  // "http://host:port": call the API at that exact origin instead - use this
  //   if the API is hosted on a different server than this frontend.
  apiBaseUrl: null
};
