const tk = {
  auth: {
    fields: {
      login: 'E-poçta ýa-da telefon',
      email: 'E-poçta',
      password: 'Açar sözi',
      passwordHint:
        'Azyndan 12 belgi. Birnäçe sözden düzülen sözlem ýatda galyjy we ygtybarly bolýar.',
      fullName: 'Adyňyz we familiýaňyz',
    },
    login: {
      title: 'Ulgama giriň',
      submit: 'Girmek',
      forgot: 'Açar sözüni unutdyňyzmy?',
      noAccount: 'Hasabyňyz ýokmy?',
      twoFactorTitle: 'Iki basgançakly tassyklama',
    },
    register: {
      title: 'Hasap açyň',
      submit: 'Hasaba alynmak',
      haveAccount: 'Hasabyňyz barmy?',
      sentTitle: 'E-poçtaňyzy barlaň',
      sentText:
        '{email} salgysyna tassyklama haty iberildi. Hatdaky salgy arkaly hasabyňyzy işjeňleşdiriň.',
    },
    forgot: {
      title: 'Açar sözüni dikeltmek',
      intro: 'E-poçta salgyňyzy giriziň. Size açar sözüni täzelemek üçin salgy ibereris.',
      submit: 'Salgyny ibermek',
      sentTitle: 'Haty barlaň',
      sentText:
        'Bu salgy bilen hasap bar bolsa, gysga wagtda açar sözüni dikeltmek üçin hat geler.',
      backToLogin: 'Girmek sahypasyna dolan',
    },
    reset: {
      title: 'Täze açar sözi',
      newPassword: 'Täze açar sözi',
      confirmPassword: 'Täze açar sözüni gaýtalaň',
      submit: 'Açar sözüni täzelemek',
      invalidTitle: 'Salgy nädogry',
      invalidText: 'Bu salgynyň möhleti geçen ýa-da ol eýýäm ulanylan. Täze salgy soraň.',
      doneTitle: 'Açar sözi täzelendi',
      doneText:
        'Howpsuzlyk üçin ähli enjamlarda ulgamdan çykaryldyňyz. Täze açar sözi bilen giriň.',
      requestNew: 'Täze salgy soramak',
    },
    confirm: {
      title: 'E-poçtany tassyklamak',
      doneTitle: 'E-poçta tassyklandy',
      doneText: 'Indi ulgama girip bilersiňiz.',
      failedTitle: 'Tassyklap bolmady',
      invalidLink: 'Bu salgy doly däl. Hatdaky salgyny gaýtadan açyp görüň.',
      resend: 'Tassyklama hatyny täzeden ibermek',
      resent: 'Hat täzeden iberildi. E-poçtaňyzy barlaň.',
    },
    twoFactor: {
      intro: 'Tassyklaýjy programmadaky 6 sanly kody giriziň.',
      recoveryIntro: 'Hasap açylanda saklan dikeldiş kodlaryňyzyň birini giriziň.',
      code: 'Tassyklama kody',
      recoveryCode: 'Dikeldiş kody',
      submit: 'Tassykla',
      useRecovery: 'Dikeldiş koduny ulan',
      useApp: 'Programmadaky kody ulan',
    },
    setup: {
      title: 'Iki basgançakly tassyklamany guruň',
      required: 'Işgär hasaplary üçin iki basgançakly tassyklama hökmany.',
      step1: 'Telefonyňyza tassyklaýjy programma guruň (Google Authenticator, Aegis ýa-da başga).',
      step2: 'Programmada aşakdaky QR kody okadyň.',
      step3: 'Programmanyň görkezen 6 sanly koduny giriziň.',
      manualKey: 'QR kody okadyp bolmasa, şu açary el bilen giriziň',
      submit: 'Işjeňleşdir',
    },
    recovery: {
      title: 'Dikeldiş kodlary',
      warning:
        'Bu kodlar diňe şu gezek görkezilýär. Telefonyňyz ýitse, hasaba diňe olar bilen girip bilersiňiz.',
      saved: 'Kodlary ygtybarly ýerde sakladym',
    },
    password: {
      title: 'Açar sözüni üýtgetmek',
      current: 'Häzirki açar sözi',
      submit: 'Açar sözüni üýtget',
      changed: 'Açar sözi üýtgedildi. Beýleki enjamlardaky sessiýalar ýapyldy.',
    },
    profile: {
      title: 'Profil',
      locale: 'Hatlaryň dili',
      localeHint: 'Size iberilýän hatlar şu dilde ýazylýar.',
      saved: 'Profil saklandy.',
    },
    emailChange: {
      title: 'E-poçta salgysy',
      current: 'Häzirki salgy: {email}',
      intro:
        'Täze salga tassyklama haty iberilýär. Hatdaky salgy açylýança häzirki e-poçtaňyz üýtgemeýär.',
      newEmail: 'Täze e-poçta',
      submit: 'Tassyklama hatyny iber',
      sent: 'Salgy boş bolsa, {email} adresine tassyklama haty gelýär. Şoňa çenli häzirki e-poçtaňyz işleýär.',
      confirmTitle: 'Täze e-poçtany tassyklamak',
      doneTitle: 'E-poçta üýtgedildi',
      doneText: 'Howpsuzlyk üçin ähli enjamlardaky sessiýalar ýapyldy. Täze salgyňyz bilen giriň.',
      failedTitle: 'E-poçtany üýtgedip bolmady',
    },
    phone: {
      title: 'Telefon belgisi',
      number: 'Telefon belgisi',
      send: 'Kod ibermek',
      sent: '{phone} belgisine kod iberildi.',
      code: 'SMS kody',
      current: 'Tassyklanan belgi: {phone}',
    },
    sessions: {
      title: 'Aktiw sessiýalar',
      intro:
        'Hasabyňyza girlen enjamlar. Tanamaýan enjamyňyzy görseňiz, ony ýapyň we açar sözüňizi üýtgediň.',
      current: 'Şu enjam',
      lastSeen: 'Soňky gezek: {date}',
      revoke: 'Sessiýany ýap',
      revokeOthers: 'Beýleki ähli sessiýalary ýap',
      revokeWarning: 'Şol enjamda ulgamdan çykarylar we täzeden girmeli bolar.',
    },
    security: {
      title: 'Howpsuzlyk',
      description: 'Açar sözüni, tassyklamany we hasabyňyza girlen enjamlary dolandyryň.',
      twoFactorTitle: 'Iki basgançakly tassyklama',
      twoFactorOn: 'Işjeň. Her girişde programmadaky kod soralýar.',
      twoFactorOff: 'Işjeň däl. Goşmaça gorag üçin ony açmagy maslahat berýäris.',
      enable: 'Işjeňleşdir',
      disable: 'Öçür',
      disableText: 'Öçürmek üçin açar sözüňizi we programmadaky kody giriziň.',
      regenerate: 'Täze dikeldiş kodlary',
      regenerateText:
        'Köne kodlar işlemegini bes eder. Tassyklamak üçin programmadaky kody giriziň.',
    },
    logout: 'Ulgamdan çykmak',
  },
}

type Messages = typeof tk

const ru: Messages = {
  auth: {
    fields: {
      login: 'Эл. почта или телефон',
      email: 'Эл. почта',
      password: 'Пароль',
      passwordHint:
        'Не менее 12 символов. Фраза из нескольких слов и надёжна, и легко запоминается.',
      fullName: 'Имя и фамилия',
    },
    login: {
      title: 'Вход в систему',
      submit: 'Войти',
      forgot: 'Забыли пароль?',
      noAccount: 'Нет учётной записи?',
      twoFactorTitle: 'Двухэтапное подтверждение',
    },
    register: {
      title: 'Создать учётную запись',
      submit: 'Зарегистрироваться',
      haveAccount: 'Уже есть учётная запись?',
      sentTitle: 'Проверьте почту',
      sentText:
        'На адрес {email} отправлено письмо. Перейдите по ссылке из него, чтобы активировать учётную запись.',
    },
    forgot: {
      title: 'Восстановление пароля',
      intro: 'Введите адрес эл. почты, и мы пришлём ссылку для смены пароля.',
      submit: 'Отправить ссылку',
      sentTitle: 'Проверьте почту',
      sentText: 'Если такая учётная запись существует, письмо со ссылкой скоро придёт.',
      backToLogin: 'Вернуться ко входу',
    },
    reset: {
      title: 'Новый пароль',
      newPassword: 'Новый пароль',
      confirmPassword: 'Повторите новый пароль',
      submit: 'Сменить пароль',
      invalidTitle: 'Ссылка недействительна',
      invalidText: 'Срок действия ссылки истёк, или она уже использована. Запросите новую.',
      doneTitle: 'Пароль изменён',
      doneText:
        'Для безопасности вы вышли из системы на всех устройствах. Войдите с новым паролем.',
      requestNew: 'Запросить новую ссылку',
    },
    confirm: {
      title: 'Подтверждение почты',
      doneTitle: 'Почта подтверждена',
      doneText: 'Теперь можно войти в систему.',
      failedTitle: 'Не удалось подтвердить',
      invalidLink: 'Ссылка неполная. Попробуйте открыть её из письма ещё раз.',
      resend: 'Отправить письмо ещё раз',
      resent: 'Письмо отправлено повторно. Проверьте почту.',
    },
    twoFactor: {
      intro: 'Введите 6-значный код из приложения-аутентификатора.',
      recoveryIntro: 'Введите один из резервных кодов, сохранённых при настройке.',
      code: 'Код подтверждения',
      recoveryCode: 'Резервный код',
      submit: 'Подтвердить',
      useRecovery: 'Использовать резервный код',
      useApp: 'Использовать код из приложения',
    },
    setup: {
      title: 'Настройка двухэтапного подтверждения',
      required: 'Для учётных записей сотрудников двухэтапное подтверждение обязательно.',
      step1:
        'Установите на телефон приложение-аутентификатор (Google Authenticator, Aegis или другое).',
      step2: 'Отсканируйте в нём QR-код ниже.',
      step3: 'Введите 6-значный код из приложения.',
      manualKey: 'Если QR-код не считывается, введите этот ключ вручную',
      submit: 'Включить',
    },
    recovery: {
      title: 'Резервные коды',
      warning:
        'Коды показываются только сейчас. Если вы потеряете телефон, войти можно будет только с ними.',
      saved: 'Я сохранил(а) коды в надёжном месте',
    },
    password: {
      title: 'Смена пароля',
      current: 'Текущий пароль',
      submit: 'Сменить пароль',
      changed: 'Пароль изменён. Сессии на других устройствах закрыты.',
    },
    profile: {
      title: 'Профиль',
      locale: 'Язык писем',
      localeHint: 'На этом языке вам будут приходить письма.',
      saved: 'Профиль сохранён.',
    },
    emailChange: {
      title: 'Адрес эл. почты',
      current: 'Текущий адрес: {email}',
      intro:
        'На новый адрес придёт письмо с подтверждением. Пока ссылка из письма не открыта, текущая почта не меняется.',
      newEmail: 'Новая эл. почта',
      submit: 'Отправить письмо',
      sent: 'Если адрес свободен, на {email} придёт письмо с подтверждением. До этого работает текущая почта.',
      confirmTitle: 'Подтверждение новой почты',
      doneTitle: 'Почта изменена',
      doneText: 'В целях безопасности сессии на всех устройствах закрыты. Войдите с новым адресом.',
      failedTitle: 'Не удалось изменить почту',
    },
    phone: {
      title: 'Номер телефона',
      number: 'Номер телефона',
      send: 'Отправить код',
      sent: 'Код отправлен на номер {phone}.',
      code: 'Код из SMS',
      current: 'Подтверждённый номер: {phone}',
    },
    sessions: {
      title: 'Активные сессии',
      intro:
        'Устройства, с которых выполнен вход. Если видите незнакомое, закройте его сессию и смените пароль.',
      current: 'Это устройство',
      lastSeen: 'Последняя активность: {date}',
      revoke: 'Закрыть сессию',
      revokeOthers: 'Закрыть все остальные сессии',
      revokeWarning: 'На том устройстве потребуется войти заново.',
    },
    security: {
      title: 'Безопасность',
      description:
        'Управляйте паролем, подтверждением входа и устройствами, с которых выполнен вход.',
      twoFactorTitle: 'Двухэтапное подтверждение',
      twoFactorOn: 'Включено. При каждом входе запрашивается код из приложения.',
      twoFactorOff: 'Выключено. Рекомендуем включить для дополнительной защиты.',
      enable: 'Включить',
      disable: 'Выключить',
      disableText: 'Чтобы выключить, введите пароль и код из приложения.',
      regenerate: 'Новые резервные коды',
      regenerateText:
        'Старые коды перестанут работать. Для подтверждения введите код из приложения.',
    },
    logout: 'Выйти',
  },
}

const en: Messages = {
  auth: {
    fields: {
      login: 'Email or phone',
      email: 'Email',
      password: 'Password',
      passwordHint:
        'At least 12 characters. A phrase of a few words is both strong and easy to remember.',
      fullName: 'Full name',
    },
    login: {
      title: 'Sign in',
      submit: 'Sign in',
      forgot: 'Forgot your password?',
      noAccount: 'No account yet?',
      twoFactorTitle: 'Two-step verification',
    },
    register: {
      title: 'Create your account',
      submit: 'Create account',
      haveAccount: 'Already have an account?',
      sentTitle: 'Check your email',
      sentText:
        'We sent a confirmation email to {email}. Open the link inside to activate your account.',
    },
    forgot: {
      title: 'Reset your password',
      intro: 'Enter your email address and we will send you a link to set a new password.',
      submit: 'Send link',
      sentTitle: 'Check your email',
      sentText: 'If an account exists for this address, an email with a link is on its way.',
      backToLogin: 'Back to sign in',
    },
    reset: {
      title: 'New password',
      newPassword: 'New password',
      confirmPassword: 'Repeat the new password',
      submit: 'Change password',
      invalidTitle: 'This link is not valid',
      invalidText: 'It has expired or was already used. Please request a new one.',
      doneTitle: 'Password changed',
      doneText: 'For your security you were signed out everywhere. Sign in with the new password.',
      requestNew: 'Request a new link',
    },
    confirm: {
      title: 'Confirm your email',
      doneTitle: 'Email confirmed',
      doneText: 'You can sign in now.',
      failedTitle: 'Could not confirm',
      invalidLink: 'This link is incomplete. Try opening it from the email again.',
      resend: 'Send the confirmation email again',
      resent: 'Email sent again. Please check your inbox.',
    },
    twoFactor: {
      intro: 'Enter the 6-digit code from your authenticator app.',
      recoveryIntro: 'Enter one of the recovery codes you saved during setup.',
      code: 'Verification code',
      recoveryCode: 'Recovery code',
      submit: 'Verify',
      useRecovery: 'Use a recovery code',
      useApp: 'Use the code from the app',
    },
    setup: {
      title: 'Set up two-step verification',
      required: 'Two-step verification is required for staff accounts.',
      step1: 'Install an authenticator app on your phone (Google Authenticator, Aegis or similar).',
      step2: 'Scan the QR code below with the app.',
      step3: 'Enter the 6-digit code it shows.',
      manualKey: 'If you cannot scan the code, enter this key by hand',
      submit: 'Turn on',
    },
    recovery: {
      title: 'Recovery codes',
      warning:
        'These codes are shown only now. If you lose your phone, they are the only way back in.',
      saved: 'I have stored the codes somewhere safe',
    },
    password: {
      title: 'Change password',
      current: 'Current password',
      submit: 'Change password',
      changed: 'Password changed. Sessions on your other devices were closed.',
    },
    profile: {
      title: 'Profile',
      locale: 'Email language',
      localeHint: 'The emails we send you are written in this language.',
      saved: 'Profile saved.',
    },
    emailChange: {
      title: 'Email address',
      current: 'Current address: {email}',
      intro:
        'A confirmation goes to the new address. Your current email stays until the link in it is opened.',
      newEmail: 'New email',
      submit: 'Send confirmation',
      sent: 'If the address is free, a confirmation is on its way to {email}. Your current email works until then.',
      confirmTitle: 'Confirm your new email',
      doneTitle: 'Email changed',
      doneText:
        'For safety, sessions on all your devices were closed. Sign in with the new address.',
      failedTitle: 'The email could not be changed',
    },
    phone: {
      title: 'Phone number',
      number: 'Phone number',
      send: 'Send code',
      sent: 'We sent a code to {phone}.',
      code: 'SMS code',
      current: 'Confirmed number: {phone}',
    },
    sessions: {
      title: 'Active sessions',
      intro:
        'Devices signed in to your account. If you see one you do not recognize, close it and change your password.',
      current: 'This device',
      lastSeen: 'Last active: {date}',
      revoke: 'Close session',
      revokeOthers: 'Close all other sessions',
      revokeWarning: 'That device will be signed out and will have to sign in again.',
    },
    security: {
      title: 'Security',
      description:
        'Manage your password, two-step verification and the devices signed in to your account.',
      twoFactorTitle: 'Two-step verification',
      twoFactorOn: 'On. Each sign-in asks for a code from your app.',
      twoFactorOff: 'Off. We recommend turning it on for extra protection.',
      enable: 'Turn on',
      disable: 'Turn off',
      disableText: 'To turn it off, enter your password and the code from your app.',
      regenerate: 'New recovery codes',
      regenerateText: 'The old codes will stop working. Enter the code from your app to confirm.',
    },
    logout: 'Sign out',
  },
}

export const authMessages = { tk, ru, en }
