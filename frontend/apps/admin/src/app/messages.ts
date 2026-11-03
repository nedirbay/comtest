const tk = {
  admin: {
    brand: 'Dolandyryş paneli',
    tagline: 'Harytlaryňyz, sargytlaryňyz we müşderileriňiz bir ýerde.',
    nav: { dashboard: 'Baş sahypa', security: 'Howpsuzlyk', menu: 'Menýu' },
    selectStore: {
      title: 'Dükany saýlaň',
      intro: 'Haýsy dükan bilen işlejekdigiňizi saýlaň.',
      none: 'Hasabyňyz entek hiç bir dükana bellenmedik. Dükan eýesinden çakylyk soraň.',
    },
    dashboard: {
      greeting: 'Hoş geldiňiz, {name}!',
      intro: 'Bu ýerde satuw we sargyt görkezijileri peýda bolar.',
    },
  },
}

const ru: typeof tk = {
  admin: {
    brand: 'Панель управления',
    tagline: 'Товары, заказы и клиенты в одном месте.',
    nav: { dashboard: 'Главная', security: 'Безопасность', menu: 'Меню' },
    selectStore: {
      title: 'Выберите магазин',
      intro: 'Укажите, с каким магазином вы будете работать.',
      none: 'Ваша учётная запись пока не привязана ни к одному магазину. Запросите приглашение у владельца.',
    },
    dashboard: {
      greeting: 'Добро пожаловать, {name}!',
      intro: 'Здесь появятся показатели продаж и заказов.',
    },
  },
}

const en: typeof tk = {
  admin: {
    brand: 'Admin panel',
    tagline: 'Your products, orders and customers in one place.',
    nav: { dashboard: 'Dashboard', security: 'Security', menu: 'Menu' },
    selectStore: {
      title: 'Choose a store',
      intro: 'Choose which store you want to work with.',
      none: 'Your account is not assigned to any store yet. Ask the store owner for an invitation.',
    },
    dashboard: {
      greeting: 'Welcome, {name}!',
      intro: 'Sales and order figures will appear here.',
    },
  },
}

export const adminMessages = { tk, ru, en }
