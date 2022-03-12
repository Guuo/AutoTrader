using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Data;
using Newtonsoft.Json.Linq;
using Deedle;

namespace AutoTrader
{
    class MainClass
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Program p = new Program();
            p.ExitRequested += p_ExitRequested;
            Task programStart = p.StartAsync();

            Application.Run();

        }
        static void p_ExitRequested(object sender, EventArgs e)
        {
            Application.ExitThread();
        }
    }

    public class Program
    {
        
        private readonly Form1 m_mainForm;
        public Program()
        { 
            DataLoader dataLoader = new DataLoader();
            m_mainForm = new Form1(dataLoader);
            m_mainForm.FormClosed += m_mainForm_FormClosed;
        }

        public event EventHandler<EventArgs> ExitRequested;
        void m_mainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            OnExitRequested(EventArgs.Empty);
            
        }
        protected virtual void OnExitRequested(EventArgs e)
        {
            if (ExitRequested != null)
                ExitRequested(this, e);
        }

        //Used synchronously here, could for instance hold an InitializeAsync() method call for async tasks at program launch.
        public async Task StartAsync()
        {
            m_mainForm.Show();
        }

        //Inactive method, left for possible debugging.
        /*
        public static async Task<Series<DateTime, decimal>> RunAsync(DataLoader dataLoader)
        {
            /*
            Console.WriteLine("Running GetJsonDataAsync via RunAsync...");
            var content = await dataLoader.GetJsonDataAsync();
            
            return content;
            
        }*/
    }

    



}
