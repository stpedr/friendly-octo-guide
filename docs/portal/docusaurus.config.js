// Portal de docs da plataforma. As páginas vêm de docs/ (arquitetura, governança)
// — fonte única, versionada com o código. `npm start` sobe em :3000 (compose
// docs-portal, perfil plataforma-eng).
// @ts-check

const githubPages = process.env.GITHUB_PAGES === 'true';

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Plataforma de Linha',
  tagline: 'Arquitetura, governança e operação — fonte única',
  favicon: 'img/favicon.ico',
  url: process.env.DOCUSAURUS_URL || 'http://localhost:3003',
  baseUrl: process.env.DOCUSAURUS_BASE_URL || '/',
  organizationName: 'stpedr',
  projectName: 'friendly-octo-guide',
  deploymentBranch: 'gh-pages',
  trailingSlash: false,
  onBrokenLinks: 'warn',
  markdown: { mermaid: true },
  i18n: { defaultLocale: 'pt-BR', locales: ['pt-BR'] },
  themes: ['@docusaurus/theme-mermaid'],
  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          // Reaproveita os docs do repo: nada de duplicar arquitetura/governança.
          path: process.env.DOCUSAURUS_DOCS_PATH || '../',
          include: ['arquitetura.md', 'mapa-implementacao.md', 'governanca/**/*.md'],
          routeBasePath: 'docs',
          sidebarPath: require.resolve('./sidebars.js'),
        },
        blog: false,
        theme: { customCss: require.resolve('./src/css/custom.css') },
      }),
    ],
  ],
  themeConfig: {
    navbar: {
      title: 'Plataforma de Linha',
      items: [
        { to: '/docs/arquitetura', label: 'Arquitetura', position: 'left' },
        { to: '/docs/mapa-implementacao', label: 'Mapa completo', position: 'left' },
        ...(githubPages
          ? [{ href: 'https://github.com/stpedr/friendly-octo-guide', label: 'GitHub', position: 'right' }]
          : []),
      ],
    },
    footer: { style: 'dark', copyright: 'Plataforma de Linha — docs internos' },
  },
};

module.exports = config;
