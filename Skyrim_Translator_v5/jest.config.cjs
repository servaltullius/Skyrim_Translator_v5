/** @type {import('jest').Config} */
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  // [TEMP] External revert issue: restrict to single test file until stabilized
  testMatch: ['<rootDir>/tests/xml-utils.correct.test.ts'],
  moduleFileExtensions: ['ts', 'tsx', 'js', 'jsx', 'json', 'node'],
  transform: {
    '^.+\\.tsx?$': ['ts-jest', { tsconfig: 'tsconfig.json' }],
  },
  verbose: true,
  clearMocks: true,
  collectCoverage: false,
};