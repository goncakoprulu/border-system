const configuredApiUrl = process.env.NEXT_PUBLIC_API_URL?.trim();

export const siteConfig = {
  apiUrl: configuredApiUrl ? configuredApiUrl.replace(/\/+$/, "") : "",
  publicSiteUrl:
    process.env.NEXT_PUBLIC_PUBLIC_SITE_URL ?? "http://border.com.tr",
} as const;
