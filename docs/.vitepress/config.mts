import { defineConfig } from 'vitepress'

// 站点挂在自定义域名根目录：GitHub Pages 绑定 wtools.opkg.cn 后，
// pikachuim.github.io/WinTools/ 会被 301 到根，所以 base 固定为 '/'，不要加 /WinTools/ 前缀
const base = '/'
const repo = 'https://github.com/PIKACHUIM/WinTools'

export default defineConfig({
  base,
  lang: 'zh-CN',
  title: 'WinTools',
  description: 'WinTools 将 Windows 清理、优化、更新、驱动管理与备份等常用维护功能集中在一个桌面程序中。',

  // 架构方案文档与规则种子数据不参与站点构建
  // 规则源与历史清单不参与站点构建
  // readme/ 是给 GitHub 用的多语言 README，链接指向仓库根目录，纳入构建会产生死链
  srcExclude: ['**/legacy/**', 'readme/**'],

  head: [
    ['link', { rel: 'icon', type: 'image/png', href: `${base}logo.png` }],
    ['meta', { name: 'theme-color', content: '#3FA9F5' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'WinTools · Windows 系统工具箱' }],
    ['meta', { property: 'og:description', content: '清理、优化、更新、驱动管理与备份等常用 Windows 维护功能，按模块集中管理。' }]
  ],

  markdown: {
    image: { lazyLoading: true },
    lineNumbers: false
  },

  themeConfig: {
    logo: '/logo.png',
    siteTitle: 'WinTools',

    nav: [
      { text: '指南', link: '/guide/introduction', activeMatch: '/guide/' },
      { text: '功能', link: '/guide/features/', activeMatch: '/guide/features/' },
      { text: '开发', link: '/guide/development' },
      { text: '参考项目', link: '/guide/reference' },
      {
        text: '下载',
        items: [
          { text: '最新版本', link: `${repo}/releases/latest` },
          { text: '更新日志', link: `${repo}/releases` },
          { text: '问题反馈', link: `${repo}/issues` }
        ]
      }
    ],

    sidebar: {
      '/guide/': [
        {
          text: '开始',
          items: [
            { text: '项目介绍', link: '/guide/introduction' },
            { text: '安装与首次使用', link: '/guide/getting-started' }
          ]
        },
        {
          text: '功能详解',
          items: [
            { text: '功能一览', link: '/guide/features/' },
            { text: '垃圾清理', link: '/guide/features/cleanup' },
            { text: '系统优化', link: '/guide/features/optimize' },
            { text: '功能管理', link: '/guide/features/system-features' },
            { text: '更新管理', link: '/guide/features/updates' },
            { text: '运行管理', link: '/guide/features/runtime' },
            { text: '组件管理', link: '/guide/features/components' },
            { text: '运行环境', link: '/guide/features/environments' },
            { text: '应用管理', link: '/guide/features/apps' },
            { text: '驱动管理', link: '/guide/features/drivers' },
            { text: '备份还原', link: '/guide/features/backup' }
          ]
        },
        {
          text: '进阶',
          items: [
            { text: '开发指引', link: '/guide/development' },
            { text: '架构说明', link: '/guide/architecture' },
            { text: '常见问题', link: '/guide/faq' },
            { text: '竞品对照矩阵', link: '/guide/competitive-analysis' },
            { text: '参考项目与致谢', link: '/guide/reference' }
          ]
        }
      ]
    },

    socialLinks: [{ icon: 'github', link: repo }],

    search: {
      provider: 'local',
      options: {
        translations: {
          button: { buttonText: '搜索文档', buttonAriaLabel: '搜索文档' },
          modal: {
            noResultsText: '没有找到结果',
            resetButtonTitle: '清除条件',
            footer: { selectText: '选择', navigateText: '切换', closeText: '关闭' }
          }
        }
      }
    },

    outline: { level: [2, 3], label: '本页目录' },
    docFooter: { prev: '上一篇', next: '下一篇' },
    lastUpdated: { text: '最后更新于', formatOptions: { dateStyle: 'short', timeStyle: 'short' } },
    darkModeSwitchLabel: '外观',
    lightModeSwitchTitle: '切换到浅色模式',
    darkModeSwitchTitle: '切换到深色模式',
    sidebarMenuLabel: '菜单',
    returnToTopLabel: '回到顶部',
    langMenuLabel: '语言',

    editLink: {
      pattern: `${repo}/edit/main/docs/:path`,
      text: '在 GitHub 上编辑此页'
    },

    footer: {
      message: '基于 AGPL-3.0 许可发布 · 使用前请备份重要数据',
      copyright: `Copyright © ${new Date().getFullYear()} WinTools`
    }
  }
})
