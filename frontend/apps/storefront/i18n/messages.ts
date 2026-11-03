const tk = {
  store: {
    nav: { home: 'Baş sahypa', account: 'Hasabym', login: 'Girmek', menu: 'Menýu' },
    home: {
      welcome: '{name} dükanyna hoş geldiňiz',
      intro: 'Katalog basym şu ýerde peýda bolar.',
      unavailableTitle: 'Dükan häzir elýeterli däl',
      unavailableText: 'Birazdan gaýtadan synanyşyň.',
    },
    footer: { contacts: 'Aragatnaşyk', hours: 'Iş wagty', rights: 'Ähli hukuklar goralan.' },
    account: {
      title: 'Hasabym',
      profile: 'Profil',
      security: 'Howpsuzlyk',
      name: 'Ady',
      email: 'E-poçta',
      phone: 'Telefon',
      notSet: 'Görkezilmedik',
    },
  },
}

const ru: typeof tk = {
  store: {
    nav: { home: 'Главная', account: 'Мой кабинет', login: 'Войти', menu: 'Меню' },
    home: {
      welcome: 'Добро пожаловать в {name}',
      intro: 'Скоро здесь появится каталог.',
      unavailableTitle: 'Магазин сейчас недоступен',
      unavailableText: 'Попробуйте чуть позже.',
    },
    footer: { contacts: 'Контакты', hours: 'Часы работы', rights: 'Все права защищены.' },
    account: {
      title: 'Мой кабинет',
      profile: 'Профиль',
      security: 'Безопасность',
      name: 'Имя',
      email: 'Эл. почта',
      phone: 'Телефон',
      notSet: 'Не указан',
    },
  },
}

const en: typeof tk = {
  store: {
    nav: { home: 'Home', account: 'My account', login: 'Sign in', menu: 'Menu' },
    home: {
      welcome: 'Welcome to {name}',
      intro: 'The catalog will appear here soon.',
      unavailableTitle: 'The store is not available right now',
      unavailableText: 'Please try again shortly.',
    },
    footer: { contacts: 'Contacts', hours: 'Opening hours', rights: 'All rights reserved.' },
    account: {
      title: 'My account',
      profile: 'Profile',
      security: 'Security',
      name: 'Name',
      email: 'Email',
      phone: 'Phone',
      notSet: 'Not set',
    },
  },
}

export const storeMessages = { tk, ru, en }
