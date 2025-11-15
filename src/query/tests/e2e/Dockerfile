FROM docker.io/library/python:3.14.0-slim-bookworm@sha256:e8ea0e4fc6f1876e7d2cfccc0071847534b1d72f2359cf0fd494006d05358faa
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
