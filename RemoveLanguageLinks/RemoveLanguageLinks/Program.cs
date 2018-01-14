using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace RemoveLanguageLinks
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new WikiClient {ClientUserAgent = "ClientWikiMaintenance/1.0" };
            var site = new WikiSite(client, Credentials.ApiEndpoint);
            await site.Initialization;
            await site.LoginAsync(Credentials.UserName, Credentials.Password);
            site.ModificationThrottler.ThrottleTime = TimeSpan.FromSeconds(1);
            var gen = new CategoryMembersGenerator(site, "猫物")
            {
                MemberTypes = CategoryMemberTypes.Page,
                PaginationSize = 50
            };
            await WorkAsync(gen.EnumPagesAsync(PageQueryOptions.FetchContent));
        }

        static async Task WorkAsync(IAsyncEnumerable<WikiPage> pages)
        {
            const string templateName = "WbClientLite/SiteLinks";
            var parser = new WikitextParser();
            var counter = 0;
            using (var ie = pages.GetEnumerator())
            {
                while (await ie.MoveNext())
                {
                    var page = ie.Current;
                    counter++;
                    Console.Write("{0}: {1} ", counter, page);
                    var root = parser.Parse(page.Content);
                    if (root.EnumDescendants().OfType<Template>().Any(t => MwParserUtility.NormalizeTitle(t.Name) == templateName))
                    {
                        Console.WriteLine("Skipped");
                        continue;
                    }
                    var langLinks = root.EnumDescendants().OfType<WikiLink>().Where(l =>
                    {
                        var wl = WikiClientLibrary.WikiLink.Parse(page.Site, l.Target.ToString());
                        return wl.InterwikiPrefix != null;
                    }).ToList();
                    // Remove old language links.
                    foreach (var link in langLinks)
                    {
                        if (link.PreviousNode is PlainText pt1 && string.IsNullOrWhiteSpace(pt1.Content))
                            pt1.Remove();
                        if (link.NextNode is PlainText pt2 && string.IsNullOrWhiteSpace(pt2.Content))
                            pt2.Remove();
                        var parent = link.ParentNode;
                        link.Remove();
                        if (!parent.EnumChildren().Any()) parent.Remove();
                    }
                    // Insert new template.
                    root.Lines.Add(new Paragraph(new PlainText("\n"), new Template(new Run(new PlainText(templateName)))));
                    page.Content = root.ToString();
                    await page.UpdateContentAsync("使用CrystalPool提供的语言链接。", true, true);
                    Console.WriteLine("Done");
                }
            }
        }

    }
}
