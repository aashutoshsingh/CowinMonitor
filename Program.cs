using System;
using System.Net.Http;
using System.Net.Mail;
using System.Net;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using Serilog;
using System.Threading;

namespace CowinMonitor
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Initialize();
            Log.Information("Started");


            var districtId = 395;
            using var client = new HttpClient();
            int exceptionCount = 0;

            while (true)
            {
                try
                {
                    for (int dayOffSet = 0; dayOffSet < 2; ++dayOffSet)
                    {
                        var emailBody = "";
                        var date = DateTime.Now.AddDays(dayOffSet).ToString("dd-MM-yyyy");

                        Log.Information($"Looking for districtId: {districtId}, Date : {date}");
                        emailBody = $"Looking for districtId: {districtId}, Date : {date}\n";

                        var response = await client.GetAsync($"https://cdn-api.co-vin.in/api/v2/appointment/sessions/public/calendarByDistrict?district_id={districtId}&date={date}");
                        string responseBody = await response.Content.ReadAsStringAsync();

                        var root = JsonSerializer.Deserialize<Root>(responseBody);

                        var availableCenters = root.centers.Where(x => x.sessions.Any(s => s.available_capacity > 0 && s.min_age_limit < 45)).ToList();

                        Log.Information($"total found : {availableCenters.Count}");

                        var details = "";

                        availableCenters.ForEach(x => details += $"Center : {x.name}, available slot : {string.Join('|', x.sessions)} \n");
                        Log.Information(details);

                        if (availableCenters.Count > 0)
                        {
                            emailBody += $"total found : {availableCenters.Count} @ {DateTime.Now} \n";
                            emailBody += details;
                            SendEmail(emailBody);
                        }
                    }
                }
                catch (Exception e)
                {
                    exceptionCount++;
                    Log.Error($"Exeception in main : {e.ToString()}");
                    if(exceptionCount == 3)
                        throw;
                }
                Thread.Sleep(60_000);
            }
        }

        private static void SendEmail(string body)
        {
            try
            {
                Log.Information("In SendEmail");
                var from  = "@gmail.com";
                var to  = "@gmail.com";
                var pwd = "";

                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(from);
                    mail.To.Add(to);
                    mail.Subject = "Vaccination slot available";
                    mail.Body = body;
                    mail.IsBodyHtml = false;
                    //mail.Attachments.Add(new Attachment("C:\\file.zip"));

                    using (SmtpClient smtp = new SmtpClient("smtp.gmail.com", 587))
                    {
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(from, pwd);
                        smtp.EnableSsl = true;
                        smtp.Send(mail);
                    }
                }

                Log.Information("Email Send");                
            }
            catch (Exception e)  
            {
                Log.Error($"Exeception while sending email : {e.ToString()}");
            }
        }

        private static void Initialize()
        {
            Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("log/CowinMonitor.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();
        }
    }
}
