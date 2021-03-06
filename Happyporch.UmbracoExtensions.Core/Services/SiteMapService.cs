﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Umbraco.Core;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Services;
using Umbraco.Web;

namespace HappyPorch.UmbracoExtensions.Core.Services
{
    public class SiteMapService
    {
        private readonly IUmbracoContextFactory _contextFactory;
        private readonly IVariationContextAccessor _variantContextAccessor;
        private readonly ILocalizationService _localizationService;

        public SiteMapService(IUmbracoContextFactory contextFactory, IVariationContextAccessor variationContextAccessor, ILocalizationService localizationService)
        {
            _contextFactory = contextFactory;
            _variantContextAccessor = variationContextAccessor;
            _localizationService = localizationService;
        }

        /// <summary>
        /// Ensure that the correct English variant context is being used.
        /// </summary>
        private void EnsureEnglishVariantContext()
        {
            var englishLanguage = _localizationService.GetAllLanguages()?.FirstOrDefault(l => l.IsoCode.InvariantStartsWith("en"));

            if (englishLanguage != null)
            {
                _variantContextAccessor.VariationContext = new VariationContext(englishLanguage.IsoCode);
            }
        }

        /// <summary>
        /// Gets a list of all the site's pages and descendant pages to be included in the site map.
        /// </summary>
        /// <param name="umbracoContext"></param>
        /// <param name="rootNodeId"></param>
        /// <returns></returns>
        public IEnumerable<IPublishedContent> GetSiteMapPages(int? rootNodeId = null)
        {
            EnsureEnglishVariantContext();

            var context = _contextFactory.EnsureUmbracoContext().UmbracoContext;

            // get all root nodes, or specific root node
            var rootNodes = rootNodeId.HasValue
                ? new List<IPublishedContent> { context.Content.GetById(rootNodeId.Value) }
                : context.Content.GetAtRoot()
                    .Where(x => (x.TemplateId > 0) && x.Value<bool>("HideInXmlsitemap") == false);

            if (!rootNodes.Any())
                return null;

            var pageList = new List<IPublishedContent>();

            foreach (var page in rootNodes)
            {
                pageList.Add(page);

                AddChildPages(page, pageList);
            }
            return pageList;
        }

        /// <summary>
        /// Recursive function for adding child pages with template and not hidden from sitemap to sitemap list.
        /// </summary>
        /// <param name="page"></param>
        /// <param name="pageList"></param>
        private void AddChildPages(IPublishedContent page, List<IPublishedContent> pageList)
        {
            var children = page.Children.Where(x => x.TemplateId > 0 && !x.Value<bool>("HideInXmlsitemap"));

            if (children?.Any() != true)
            {
                return;
            }

            pageList.AddRange(children);

            foreach (var childPage in children)
            {
                AddChildPages(childPage, pageList);
            }
        }

        /// <summary>
        /// Gets an XML site map of all the website's pages and descendant pages.
        /// </summary>
        /// <param name="umbracoContext"></param>
        /// <param name="rootNodeId"></param>
        /// <returns></returns>
        public XDocument GetSiteMapXml(int? rootNodeId = null)
        {
            // create the XML sitemap
            var doc = new XDocument
            {
                Declaration = new XDeclaration("1.0", "utf-8", null)
            };

            // create URL set
            XNamespace xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";

            var urlSet = new XElement(XName.Get("urlset", xmlns.NamespaceName));

            // get all pages for sitemap
            var pages = GetSiteMapPages(rootNodeId).ToList();

            if (pages.Any())
            {
                // add each page as a Url element
                foreach (var page in pages)
                {
                    foreach (var culture in page.Cultures)
                    {
                        var pageUrl = page.Url(culture.Key, mode: UrlMode.Absolute);

                        if (pageUrl == "#")
                        {
                            // skip any culture pages without a valid URL
                            continue;
                        }

                        var url = new XElement(xmlns + "url");

                        url.Add(new XElement(xmlns + "loc", pageUrl));
                        url.Add(new XElement(xmlns + "lastmod", page.UpdateDate.ToString("yyyy-MM-dd")));

                        urlSet.Add(url);
                    }
                }
            }
            doc.Add(urlSet);

            return doc;
        }

        /// <summary>
        /// Gets an XML site map of all the site's pages and descendant pages in a UTF-8 encoded string.
        /// </summary>
        /// <param name="umbracoContext"></param>
        /// <param name="rootNodeId"></param>
        /// <returns></returns>
        public string GetSiteMapXmlString(int? rootNodeId = null)
        {
            var doc = GetSiteMapXml(rootNodeId);

            return $"{doc.Declaration}{Environment.NewLine}{doc}";
        }
    }
}
