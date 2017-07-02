#! /usr/bin/env node

// @ts-check

const { runBaselineAndInferKind } = require("./runBaseline");

const args = process.argv.slice(2);
const testName = args[0];

if (testName === undefined) throw new Error();

console.log(runBaselineAndInferKind(testName));
