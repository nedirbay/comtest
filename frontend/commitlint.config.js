export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    // Commit subjects are written in Turkmen as well as English, and Turkmen sentences
    // can start with a proper noun.
    'subject-case': [0],
  },
}
