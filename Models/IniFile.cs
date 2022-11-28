using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

// Change this to match your program's normal namespace
namespace DisGram.Models
{
    public class IniFile   // revision 11
    {
        string Path;
        string EXE = Assembly.GetExecutingAssembly().GetName().Name;

        public IniFile()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var folderPath = Environment.GetFolderPath(folder);
            if (!Directory.Exists(Environment.GetFolderPath(folder) + "\\Disgram"))
            {
                Directory.CreateDirectory(Environment.GetFolderPath(folder) + "\\Disgram");
            }
            Path = System.IO.Path.Join(folderPath + "\\Disgram\\Disgram.ini");

            InitIni();
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string Section, string Key, string Value, string FilePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

        public IniFile(string? IniPath = null)
        {
            Path = new FileInfo(IniPath ?? EXE + ".ini").FullName;
        }

        public string Read(string Key, string? Section = null)
        {
            var RetVal = new StringBuilder(255);
            GetPrivateProfileString(Section ?? EXE, Key, "", RetVal, 255, Path);
            return RetVal.ToString();
        }

        public void Write(string? Key, string? Value, string? Section = null)
        {
            WritePrivateProfileString(Section ?? EXE, Key, Value, Path);
        }

        public void DeleteKey(string? Key, string? Section = null)
        {
            Write(Key, null, Section ?? EXE);
        }

        public void DeleteSection(string? Section = null)
        {
            Write(null, null, Section ?? EXE);
        }

        public bool KeyExists(string? Key, string? Section = null)
        {
            return Read(Key, Section).Length > 0;
        }

        private void InitIni()
        {
            if (!KeyExists("FirstRun", "App_Settings"))
            {
                Write("FirstRun", "true", "App_Settings");
            }
            if (!KeyExists("DiscordGuild", "App_Settings"))
            {
                Write("DiscordGuild", "", "App_Settings");
            }    
            if (!KeyExists("StartText", "Telegram_Text"))
            {
                Write("StartText", "Welcome to Disgram Bot", "Telegram_Text");
            }
            if (!KeyExists("StartButtonLabel", "Telegram_Text"))
            {
                Write("StartButtonLabel", "Start chatting", "Telegram_Text");
            }
            if (!KeyExists("StartButtonResponse", "Telegram_Text"))
            {
                Write("StartButtonResponse", "Chat request sent", "Telegram_Text");
            }
            if (!KeyExists("TicketClosedByUser", "Telegram_Text"))
            {
                Write("TicketClosedByUser", "You close your ticket", "Telegram_Text");
            }
            if (!KeyExists("TicketClosedByStaff", "Telegram_Text"))
            {
                Write("TicketClosedByStaff", "Your ticket has been closed by our team", "Telegram_Text");
            }            
            if (!KeyExists("Telegram", "API_Key"))
            {
                Write("Telegram", "", "API_Key");
            }
            if (!KeyExists("Discord", "API_Key"))
            {
                Write("Discord", "", "API_Key");
            }
            if (!KeyExists("Enabled", "Presence"))
            {
                Write("Enabled", "False", "Presence");
            }
            if (!KeyExists("UnavailableText", "Presence"))
            {
                Write("UnavailableText", "Sorry no one is currently available", "Presence");
            }
        }
    }
}